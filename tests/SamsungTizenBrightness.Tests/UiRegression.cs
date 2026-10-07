// SPDX-FileCopyrightText: 2026 dttutty
// SPDX-License-Identifier: GPL-3.0-only

using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using SamsungTizenBrightness;

internal static class UiRegression
{
    internal static void CheckSetupLayout(string? outputFolder = null)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            UiLanguage previous = L.Current;
            try
            {
                Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
                Application.EnableVisualStyles();
                foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
                {
                    L.Initialize(language.ToString());
                    using Form dialog = ConnectionSetupPrompt.CreateDialog("192.0.2.1", false);
                    CreateHandles(dialog);
                    dialog.PerformLayout();
                    foreach (Label label in Labels(dialog))
                    {
                        // AutoSize headings size themselves to their text. Only
                        // fixed-size, word-wrapped instructions can clip vertically.
                        if (label.AutoSize) continue;
                        Size text = TextRenderer.MeasureText(label.Text, label.Font,
                            new Size(label.ClientSize.Width, int.MaxValue),
                            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
                        if (text.Height > label.ClientSize.Height)
                            throw new Exception($"{language} setup label clipped: {label.Text}; {text.Height}>{label.Height}");
                    }
                    if (((CheckBox)dialog.Controls["StartWithWindows"]!).Checked)
                        throw new Exception("Setup unexpectedly enabled startup.");
                    if (((TextBox)dialog.Controls["DisplayHost"]!).Text != "192.0.2.1")
                        throw new Exception("Setup lost the saved host.");
                    if (outputFolder is not null)
                    {
                        Directory.CreateDirectory(outputFolder);
                        // Own test window only, kept off-screen; no desktop capture.
                        dialog.StartPosition = FormStartPosition.Manual;
                        dialog.Location = new Point(-32000, -32000);
                        dialog.ShowInTaskbar = false;
                        dialog.Show();
                        dialog.Update();
                        using var bitmap = new Bitmap(dialog.Width, dialog.Height);
                        dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
                        bitmap.Save(Path.Combine(outputFolder, $"setup-{language}.png"), ImageFormat.Png);
                        dialog.Hide();
                    }
                }
            }
            catch (Exception error) { failure = error; }
            finally { L.Initialize(previous.ToString()); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(10))) throw new TimeoutException("Offline UI regression timed out.");
        if (failure is not null) throw failure;
    }

    private static IEnumerable<Label> Labels(Control control)
    {
        foreach (Control child in control.Controls)
        {
            if (child is Label label) yield return label;
            foreach (Label nested in Labels(child)) yield return nested;
        }
    }

    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls) CreateHandles(child);
    }
}
