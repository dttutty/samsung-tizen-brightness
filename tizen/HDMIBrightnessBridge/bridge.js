(function () {
  'use strict';

  // Replace this address with the LAN IPv4 address of the Windows PC before
  // packaging the Tizen application.
  var PC_SOCKET = 'ws://192.168.1.100:8765/m70b';
  var TV_VIEWER_APP_ID = 'org.tizen.tv-viewer';
  var MIN_BACKLIGHT = 0;
  var MAX_BACKLIGHT = 50;
  var socket = null;
  var reconnectTimer = null;
  var connectionWatchdog = null;
  var disconnectExitTimer = null;
  var videoWindowTimer = null;
  var pendingValue = null;
  var pendingId = null;
  var applyTimer = null;
  var capabilityCache = null;
  var videoSourceMap = {};
  var videoSourceOptions = [];
  var sourceChangeBusy = false;
  var closing = false;
  var statusElement = document.getElementById('status');

  // Protocol 2 never accepts a method name from the network. Every setting is
  // explicitly allow-listed here so a malformed or hostile message cannot call
  // an arbitrary Samsung Product API method.
  var settings = [
    integerSetting('backlight', 'webapis.avinfo', 'getBacklight', 'setBacklight', 0, 50, false),
    integerSetting('brightness', 'webapis.avinfo', 'getBrightness', 'setBrightness', -5, 5, true),
    integerSetting('contrast', 'webapis.avinfo', 'getContrast', 'setContrast', 0, 50, true),
    integerSetting('colorStrength', 'webapis.avinfo', 'getColorStrength', 'setColorStrength', 0, 50, true),
    integerSetting('colorTint', 'webapis.avinfo', 'getColorTint', 'setColorTint', 0, 30, true),
    integerSetting('sharpness', 'webapis.avinfo', 'getSharpness', 'setSharpness', 0, 20, true),
    integerSetting('volume', 'tizen.tvaudiocontrol', 'getVolume', 'setVolume', 0, 100),
    booleanSetting('mute', 'tizen.tvaudiocontrol', 'isMute', 'setMute'),
    enumSetting('energySaving', 'webapis.avinfo', 'getEnergySaving', 'setEnergySaving',
      ['OFF', 'LOW', 'MEDIUM', 'HIGH']),
    enumSetting('ecoSensor', 'webapis.avinfo', 'getEcoSensor', 'setEcoSensor',
      ['OFF', 'ON'])
  ];

  function setStatus(text) {
    statusElement.textContent = text;
    console.log('[HdmiBrightnessBridge] ' + text);
  }

  function errorText(error) {
    if (!error) return 'unknown error';
    return (error.name ? error.name + ': ' : '') + (error.message || String(error));
  }

  function apiObject(path) {
    if (path === 'webapis.avinfo') {
      return window.webapis && webapis.avinfo ? webapis.avinfo : null;
    }
    if (path === 'tizen.tvaudiocontrol') {
      return window.tizen && tizen.tvaudiocontrol ? tizen.tvaudiocontrol : null;
    }
    return null;
  }

  function integerSetting(id, api, getter, setter, min, max, experimental) {
    return {
      id: id,
      type: 'integer',
      api: api,
      getter: getter,
      setter: setter,
      min: min,
      max: max,
      step: 1,
      experimental: experimental === true
    };
  }

  function booleanSetting(id, api, getter, setter) {
    return {
      id: id,
      type: 'boolean',
      api: api,
      getter: getter,
      setter: setter
    };
  }

  function enumSetting(id, api, getter, setter, values) {
    return {
      id: id,
      type: 'enum',
      api: api,
      getter: getter,
      setter: setter,
      values: values
    };
  }

  function findSetting(id) {
    for (var index = 0; index < settings.length; index += 1) {
      if (settings[index].id === id) return settings[index];
    }
    return null;
  }

  function readSetting(setting) {
    var api = apiObject(setting.api);
    if (!api || typeof api[setting.getter] !== 'function') {
      throw new Error(setting.getter + ' is not available');
    }
    return api[setting.getter]();
  }

  function writeSetting(setting, value) {
    var api = apiObject(setting.api);
    if (!api || typeof api[setting.setter] !== 'function') {
      throw new Error(setting.setter + ' is not available');
    }
    var result = api[setting.setter](value);
    if (result === false) throw new Error(setting.setter + ' returned false');
  }

  function capabilityFor(setting) {
    var capability = {
      id: setting.id,
      type: setting.type,
      readable: false,
      writable: false
    };
    if (setting.experimental) capability.experimental = true;
    if (typeof setting.min === 'number') capability.min = setting.min;
    if (typeof setting.max === 'number') capability.max = setting.max;
    if (typeof setting.step === 'number') capability.step = setting.step;
    if (setting.values) capability.values = setting.values.slice(0);

    var value;
    try {
      value = readSetting(setting);
      value = validateSettingValue(setting, capability, value);
      capability.readable = true;
      capability.value = value;
    } catch (readError) {
      capability.error = errorText(readError);
      return capability;
    }

    // Capability discovery is strictly read-only. A setter is advertised only
    // when this fixed allow-list entry exists and the method is present.
    var api = apiObject(setting.api);
    capability.writable = !!(api && typeof api[setting.setter] === 'function');
    if (capability.writable) capability.writeVerified = 'method-present';
    return capability;
  }

  function getCapabilities() {
    if (capabilityCache !== null) return capabilityCache;
    capabilityCache = [];
    for (var index = 0; index < settings.length; index += 1) {
      var capability = capabilityFor(settings[index]);
      // Read failures remain in the capability response for diagnostics, while
      // the Windows UI shows only settings that also have a current value.
      capabilityCache.push(capability);
    }
    return capabilityCache;
  }

  function capabilityById(id) {
    if (id === 'inputSource') return inputSourceCapability();
    var capabilities = getCapabilities();
    for (var index = 0; index < capabilities.length; index += 1) {
      if (capabilities[index].id === id) return capabilities[index];
    }
    return null;
  }

  function videoSourceKey(source) {
    if (!source || typeof source.type !== 'string' || source.type.length === 0) return null;
    var number = Number(source.number);
    if (isFinite(number) && Math.round(number) === number && number >= 0) {
      return source.type + ' ' + number;
    }
    if (typeof source.name === 'string' && source.name.length > 0) return source.name;
    return source.type;
  }

  function addVideoSource(source) {
    var key = videoSourceKey(source);
    if (!key || videoSourceMap[key]) return;
    videoSourceMap[key] = source;
    videoSourceOptions.push({
      value: key,
      type: source.type,
      number: source.number,
      label: typeof source.name === 'string' && source.name.length > 0
        ? source.name
        : source.type + ' ' + source.number
    });
  }

  function discoverVideoSources(done) {
    videoSourceMap = {};
    videoSourceOptions = [];
    var finished = false;
    var timer = setTimeout(finish, 1800);

    function finish() {
      if (finished) return;
      finished = true;
      clearTimeout(timer);
      try {
        addVideoSource(tizen.tvwindow.getSource('MAIN'));
      } catch (ignore) {}
      done();
    }

    try {
      tizen.systeminfo.getPropertyValue(
        'VIDEOSOURCE',
        function (videoSourceInfo) {
          var connected = videoSourceInfo && videoSourceInfo.connected;
          if (connected && typeof connected.length === 'number') {
            for (var index = 0; index < connected.length; index += 1) {
              addVideoSource(connected[index]);
            }
          }
          finish();
        },
        finish
      );
    } catch (error) {
      finish();
    }
  }

  function inputSourceCapability() {
    if (!window.tizen || !tizen.tvwindow ||
        typeof tizen.tvwindow.getSource !== 'function' ||
        typeof tizen.tvwindow.setSource !== 'function') {
      return null;
    }
    var currentKey = null;
    try {
      var currentSource = tizen.tvwindow.getSource('MAIN');
      currentKey = videoSourceKey(currentSource);
      if (currentKey) addVideoSource(currentSource);
    } catch (ignore) {}
    if (!currentKey && videoSourceOptions.length > 0) {
      currentKey = videoSourceOptions[0].value;
    }
    if (!currentKey) return null;
    return {
      id: 'inputSource',
      type: 'enum',
      readable: true,
      writable: true,
      value: currentKey,
      values: videoSourceOptions.map(function (option) { return option.value; }),
      options: videoSourceOptions.slice(0),
      risk: 'display-loss',
      confirmation: true,
      writeVerified: 'method-present'
    };
  }

  function readInputSource() {
    var source = tizen.tvwindow.getSource('MAIN');
    var key = videoSourceKey(source);
    if (!key) throw new Error('TVWindow returned an invalid source');
    return key;
  }

  function rollbackVideoSource(previousSource, done) {
    var previousKey = videoSourceKey(previousSource);
    if (!previousKey || !window.tizen || !tizen.tvwindow ||
        typeof tizen.tvwindow.setSource !== 'function') {
      done({ attempted: false, succeeded: false });
      return;
    }

    var completed = false;
    var timer = setTimeout(function () {
      finish(false, 'rollback timed out');
    }, 3000);

    function finish(succeeded, message) {
      if (completed) return;
      completed = true;
      clearTimeout(timer);
      var result = {
        attempted: true,
        succeeded: succeeded,
        previousValue: previousKey
      };
      if (message) result.message = message;
      done(result);
    }

    try {
      tizen.tvwindow.setSource(
        previousSource,
        function () { finish(true); },
        function (error) { finish(false, errorText(error)); },
        'MAIN'
      );
    } catch (error) {
      finish(false, errorText(error));
    }
  }

  function sendInputSourceFailure(id, code, message, recovery) {
    send({
      op: 'error',
      id: id || null,
      code: code,
      message: message,
      setting: 'inputSource',
      risk: 'display-loss',
      recovery: recovery
    });
  }

  function recoverDisplayCapability() {
    return {
      id: 'recover_display',
      type: 'action',
      available: !!(window.tizen && tizen.application &&
        typeof tizen.application.launch === 'function'),
      risk: 'display-recovery',
      confirmation: false,
      fixedTarget: true
    };
  }

  function recoverDisplay(id, appId) {
    if (typeof appId !== 'undefined') {
      sendProtocolError(id, 'invalid_message',
        'recover_display does not accept an appId');
      return;
    }
    if (!recoverDisplayCapability().available) {
      sendProtocolError(id, 'not_available', 'TV viewer recovery is not available');
      return;
    }
    try {
      tizen.application.launch(
        TV_VIEWER_APP_ID,
        function () {
          send({
            op: 'action_ack',
            id: id || null,
            action: 'recover_display',
            risk: 'display-recovery'
          });
        },
        function (error) {
          sendProtocolError(id, 'recovery_failed', errorText(error));
        }
      );
    } catch (error) {
      sendProtocolError(id, 'recovery_failed', errorText(error));
    }
  }

  function applyInputSource(id, value, confirmed) {
    var capability = inputSourceCapability();
    if (!capability) {
      sendProtocolError(id, 'not_available', 'input source control is not available', 'inputSource');
      return;
    }
    if (confirmed !== true) {
      sendProtocolError(id, 'confirmation_required',
        'this setting can interrupt the visible HDMI signal', 'inputSource');
      return;
    }
    if (typeof value !== 'string' || !videoSourceMap[value] ||
        capability.values.indexOf(value) < 0) {
      sendProtocolError(id, 'invalid_value',
        'source must be one of the values advertised by capabilities', 'inputSource');
      return;
    }
    if (sourceChangeBusy) {
      sendProtocolError(id, 'busy', 'an input source change is already in progress', 'inputSource');
      return;
    }

    var previousSource;
    try {
      previousSource = tizen.tvwindow.getSource('MAIN');
      if (!videoSourceKey(previousSource)) {
        throw new Error('TVWindow returned an invalid current source');
      }
    } catch (error) {
      sendProtocolError(id, 'read_before_write_failed', errorText(error), 'inputSource');
      return;
    }

    sourceChangeBusy = true;
    var completed = false;
    var timer = setTimeout(function () {
      finishError('write_timeout', 'TVWindow.setSource did not complete within 8 seconds');
    }, 8000);

    function claimCompletion() {
      if (completed) return false;
      completed = true;
      clearTimeout(timer);
      return true;
    }

    function finishError(code, message) {
      if (!claimCompletion()) return;
      rollbackVideoSource(previousSource, function (recovery) {
        sourceChangeBusy = false;
        sendInputSourceFailure(id, code, message, recovery);
      });
    }

    try {
      tizen.tvwindow.setSource(
        videoSourceMap[value],
        function () {
          if (!claimCompletion()) return;
          sourceChangeBusy = false;
          try {
            send({
              op: 'setting_ack',
              id: id || null,
              setting: 'inputSource',
              value: readInputSource(),
              risk: 'display-loss'
            });
          } catch (error) {
            sendProtocolError(id, 'read_after_write_failed', errorText(error), 'inputSource');
          }
        },
        function (error) {
          finishError('write_failed', errorText(error));
        },
        'MAIN'
      );
    } catch (error) {
      finishError('write_failed', errorText(error));
    }
  }

  function validateSettingValue(setting, capability, value) {
    if (setting.type === 'integer') {
      if (typeof value !== 'number' || !isFinite(value) || Math.round(value) !== value) {
        throw new Error('value must be a finite integer');
      }
      if (value < setting.min || value > setting.max) {
        throw new Error('value must be between ' + setting.min + ' and ' + setting.max);
      }
      return value;
    }
    if (setting.type === 'boolean') {
      if (typeof value !== 'boolean') throw new Error('value must be boolean');
      return value;
    }

    if (setting.type === 'enum') {
      if (typeof value !== 'string' || setting.values.indexOf(value) < 0) {
        throw new Error('value must be one of: ' + setting.values.join(', '));
      }
      return value;
    }
    throw new Error('unsupported setting value type');
  }

  function sendProtocolError(id, code, message, setting) {
    var payload = { op: 'error', id: id || null, code: code, message: message };
    if (setting) payload.setting = setting;
    send(payload);
  }

  function sendCapabilities(id) {
    var capabilities = getCapabilities().slice(0);
    var inputSource = inputSourceCapability();
    if (inputSource) capabilities.push(inputSource);
    send({
      op: 'capabilities',
      id: id || null,
      protocol: 2,
      bridgeVersion: '2.2.8',
      settings: capabilities,
      actions: [recoverDisplayCapability()]
    });
  }

  function sendSettingState(id, settingId) {
    if (settingId === 'inputSource') {
      var inputSource = inputSourceCapability();
      if (!inputSource) {
        sendProtocolError(id, 'not_readable', 'input source is not readable', settingId);
        return;
      }
      send({ op: 'setting_state', id: id || null, setting: settingId, value: inputSource.value });
      return;
    }
    var setting = findSetting(settingId);
    var capability = capabilityById(settingId);
    if (!setting || !capability) {
      sendProtocolError(id, 'unknown_setting', 'unsupported setting', settingId);
      return;
    }
    if (!capability.readable) {
      sendProtocolError(id, 'not_readable', capability.error || 'setting is not readable', settingId);
      return;
    }
    try {
      send({ op: 'setting_state', id: id || null, setting: settingId, value: readSetting(setting) });
    } catch (error) {
      sendProtocolError(id, 'read_failed', errorText(error), settingId);
    }
  }

  function sendSettingsState(id) {
    var values = {};
    var capabilities = getCapabilities();
    for (var index = 0; index < capabilities.length; index += 1) {
      if (!capabilities[index].readable) continue;
      var setting = findSetting(capabilities[index].id);
      if (!setting) continue;
      try {
        values[setting.id] = readSetting(setting);
      } catch (ignore) {
        // A setting can become temporarily unavailable after a picture-mode or
        // source change. Omit it from this snapshot; a later request can retry.
      }
    }
    var inputSource = inputSourceCapability();
    if (inputSource && inputSource.readable) values.inputSource = inputSource.value;
    send({ op: 'settings_state', id: id || null, values: values });
  }

  function applySetting(id, settingId, value, confirmed) {
    if (settingId === 'inputSource') {
      applyInputSource(id, value, confirmed);
      return;
    }
    var setting = findSetting(settingId);
    var capability = capabilityById(settingId);
    if (!setting || !capability) {
      sendProtocolError(id, 'unknown_setting', 'unsupported setting', settingId);
      return;
    }
    if (!capability.writable) {
      sendProtocolError(id, 'not_writable', capability.writeError || 'setting is not writable', settingId);
      return;
    }
    try {
      value = validateSettingValue(setting, capability, value);
      writeSetting(setting, value);
      send({ op: 'setting_ack', id: id || null, setting: settingId, value: readSetting(setting) });
    } catch (error) {
      sendProtocolError(id, 'write_failed', errorText(error), settingId);
    }
  }

  function send(payload) {
    if (socket && socket.readyState === WebSocket.OPEN) {
      socket.send(JSON.stringify(payload));
    }
  }

  function readBacklight() {
    return webapis.avinfo.getBacklight();
  }

  function sendState(id) {
    try {
      send({
        op: 'state',
        id: id || null,
        connected: true,
        value: readBacklight(),
        min: MIN_BACKLIGHT,
        max: MAX_BACKLIGHT
      });
    } catch (error) {
      send({ op: 'error', id: id || null, message: errorText(error) });
    }
  }

  function applyPendingBacklight() {
    applyTimer = null;
    if (pendingValue === null) return;

    var value = pendingValue;
    var id = pendingId;
    pendingValue = null;
    pendingId = null;

    try {
      webapis.avinfo.setBacklight(value);
      send({ op: 'ack', id: id || null, value: readBacklight() });
    } catch (error) {
      send({ op: 'error', id: id || null, message: errorText(error) });
    }
  }

  function queueBacklight(value, id) {
    if (typeof value !== 'number' || !isFinite(value)) {
      send({ op: 'error', id: id || null, message: 'value must be a finite number' });
      return;
    }

    pendingValue = Math.max(MIN_BACKLIGHT, Math.min(MAX_BACKLIGHT, Math.round(value)));
    pendingId = id || null;
    if (applyTimer === null) applyTimer = setTimeout(applyPendingBacklight, 80);
  }

  function handleMessage(event) {
    var message;
    try {
      message = JSON.parse(event.data);
    } catch (error) {
      send({ op: 'error', message: 'invalid JSON' });
      return;
    }

    if (!message || typeof message !== 'object') {
      sendProtocolError(null, 'invalid_message', 'message must be an object');
    } else if (message.op === 'get') {
      sendState(message.id);
    } else if (message.op === 'set') {
      queueBacklight(message.value, message.id);
    } else if (message.op === 'capabilities') {
      sendCapabilities(message.id);
    } else if (message.op === 'get_setting') {
      if (typeof message.setting !== 'string') {
        sendProtocolError(message.id, 'invalid_setting', 'setting must be a string');
      } else {
        sendSettingState(message.id, message.setting);
      }
    } else if (message.op === 'get_settings') {
      sendSettingsState(message.id);
    } else if (message.op === 'set_setting') {
      if (typeof message.setting !== 'string') {
        sendProtocolError(message.id, 'invalid_setting', 'setting must be a string');
      } else {
        applySetting(message.id, message.setting, message.value, message.confirmed);
      }
    } else if (message.op === 'recover_display') {
      recoverDisplay(message.id, message.appId);
    } else if (message.op === 'ping') {
      send({ op: 'pong', id: message.id || null });
    } else if (message.op === 'exit') {
      send({ op: 'ack', id: message.id || null, value: readBacklight() });
      setTimeout(closeApp, 100);
    } else {
      sendProtocolError(message.id, 'unsupported_operation', 'unsupported operation');
    }
  }

  function connectToPc() {
    if (closing) return;
    clearTimeout(reconnectTimer);
    if (socket && (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING)) {
      return;
    }
    try {
      socket = new WebSocket(PC_SOCKET);
      socket.onopen = function () {
        clearTimeout(disconnectExitTimer);
        disconnectExitTimer = null;
        send({
          op: 'hello',
          role: 'tv',
          model: 'Samsung Tizen',
          protocol: 1,
          protocolMax: 2,
          features: ['settings-v2', 'high-risk-settings', 'display-recovery']
        });
        sendState();
      };
      socket.onmessage = handleMessage;
      socket.onerror = function () {
        try { socket.close(); } catch (ignore) {}
      };
      socket.onclose = function () {
        socket = null;
        if (closing) return;
        scheduleDisconnectExit();
        reconnectTimer = setTimeout(connectToPc, 2000);
      };
    } catch (error) {
      scheduleDisconnectExit();
      reconnectTimer = setTimeout(connectToPc, 2000);
    }
  }

  function scheduleDisconnectExit() {
    if (closing || disconnectExitTimer !== null) return;
    disconnectExitTimer = setTimeout(function () {
      disconnectExitTimer = null;
      setStatus('电脑连接已断开，正在退出桥接器以恢复电视自动关机。');
      closeApp();
    }, 30000);
  }

  function showWindow() {
    clearTimeout(videoWindowTimer);
    videoWindowTimer = setTimeout(function () {
      setStatus('显示设备没有响应 TVWindow.show；请按返回键退出。');
      document.getElementById('close').focus();
    }, 8000);
    tizen.tvwindow.show(
      function () {
        clearTimeout(videoWindowTimer);
        setStatus('HDMI 画面已显示；等待电脑连接。');
        discoverVideoSources(function () {
          connectToPc();
          connectionWatchdog = setInterval(connectToPc, 2000);
        });
      },
      function (error) {
        clearTimeout(videoWindowTimer);
        setStatus('无法显示 HDMI：' + errorText(error));
        document.getElementById('close').focus();
      },
      ['0%', '0%', '100%', '100%'],
      'MAIN',
      'FRONT'
    );
  }

  function start() {
    if (!window.tizen || !tizen.tvwindow) {
      setStatus('此设备不支持 TVWindow API。');
      return;
    }
    if (!window.webapis || !webapis.avinfo || typeof webapis.avinfo.setBacklight !== 'function') {
      setStatus('此设备没有可用的 AVInfo 背光接口。');
      return;
    }
    try {
      if (!tizen.systeminfo.getCapability('http://tizen.org/feature/tv.pip')) {
        setStatus('显示设备报告不支持 TVWindow/PiP。');
        document.getElementById('close').focus();
        return;
      }
    } catch (ignore) {}

    // Startup always preserves the source selected by the user. Source changes
    // are possible only through an explicit, confirmed protocol 2 request.
    showWindow();
  }

  function closeApp() {
    if (closing) return;
    closing = true;
    clearTimeout(reconnectTimer);
    clearTimeout(videoWindowTimer);
    clearTimeout(disconnectExitTimer);
    clearInterval(connectionWatchdog);
    if (applyTimer !== null) clearTimeout(applyTimer);
    if (socket) {
      try { socket.close(); } catch (ignore) {}
    }
    try { tizen.application.getCurrentApplication().exit(); }
    catch (error) { window.close(); }
  }

  document.getElementById('close').addEventListener('click', closeApp);
  document.addEventListener('keydown', function (event) {
    if (event.keyCode === 10009 || event.key === 'Escape') closeApp();
  });

  start();
}());
