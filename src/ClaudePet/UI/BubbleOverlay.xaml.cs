using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace ClaudePet.UI;

public partial class BubbleOverlay : UserControl
{
    public event Action<string>? MessageSent;
    public event Action? CloseRequested;
    public event Action? VoiceRequested;

    public BubbleOverlay()
    {
        InitializeComponent();
    }

    public void AddUserMessage(string text)
    {
        Dispatcher.Invoke(() =>
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                MaxWidth = 240
            };
            MessagesPanel.Children.Add(tb);
            ChatScroll.ScrollToEnd();
        });
    }

    public void AddMessage(string text)
    {
        Dispatcher.Invoke(() =>
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x20, 0x4C, 0xAF, 0x50)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 8),
                MaxWidth = 270
            };

            border.Child = RenderMarkdown(text);
            MessagesPanel.Children.Add(border);
            ChatScroll.ScrollToEnd();
        });
    }

    public void AddSystemMessage(string text)
    {
        Dispatcher.Invoke(() =>
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            MessagesPanel.Children.Add(tb);
            ChatScroll.ScrollToEnd();
        });
    }

    /// <summary>简单 Markdown → WPF 格式化文本</summary>
    private static readonly List<string> _tableBuffer = new();

    private static void FlushTable(StackPanel panel)
    {
        if (_tableBuffer.Count == 0) return;

        // 过滤掉分隔行 (|---|---|)
        var rows = _tableBuffer
            .Select(line => line.Trim().Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim()).ToArray())
            .Where(cols => !cols.All(c => c.All(ch => ch == '-' || ch == ':')))
            .ToList();

        if (rows.Count == 0) { _tableBuffer.Clear(); return; }

        int colCount = rows.Max(r => r.Length);
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 8) };
        for (int i = 0; i < colCount; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        for (int r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            bool isHeader = r == 0;
            for (int c = 0; c < colCount; c++)
            {
                var cellText = c < rows[r].Length ? rows[r][c] : "";
                var border = new Border
                {
                    Background = isHeader
                        ? new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
                        : new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(6, 2, 6, 2)
                };
                border.Child = new TextBlock
                {
                    Text = cellText,
                    FontSize = 10,
                    Foreground = isHeader
                        ? new SolidColorBrush(Colors.White)
                        : new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                    FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal
                };
                Grid.SetColumn(border, c);
                Grid.SetRow(border, r);
                grid.Children.Add(border);
            }
        }

        panel.Children.Add(grid);
        _tableBuffer.Clear();
    }

    private static FrameworkElement RenderMarkdown(string md)
    {
        var panel = new StackPanel();
        bool inCodeBlock = false;
        string codeLang = "";
        var codeLines = new List<string>();
        _tableBuffer.Clear();

        foreach (var rawLine in md.Split('\n'))
        {
            var line = rawLine.TrimEnd();

            // 代码块
            if (line.TrimStart().StartsWith("```"))
            {
                FlushTable(panel);
                if (inCodeBlock)
                {
                    // 结束代码块
                    if (codeLines.Count > 0)
                    {
                        var codeBorder = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(8, 4, 8, 4),
                            Margin = new Thickness(0, 2, 0, 6)
                        };
                        var codeText = new TextBlock
                        {
                            Text = string.Join("\n", codeLines),
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromRgb(0xCE, 0xCE, 0xCE)),
                            FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New"),
                            TextWrapping = TextWrapping.Wrap
                        };
                        codeBorder.Child = codeText;
                        panel.Children.Add(codeBorder);
                        codeLines.Clear();
                    }
                    inCodeBlock = false;
                }
                else
                {
                    inCodeBlock = true;
                    codeLang = line.TrimStart().TrimStart('`').Trim();
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeLines.Add(line);
                continue;
            }

            // 空行（跳过，表格内空行不冲掉缓冲）
            if (string.IsNullOrWhiteSpace(line))
            {
                if (_tableBuffer.Count > 0) continue; // 表格内空行忽略
                panel.Children.Add(new TextBlock { Height = 4 });
                continue;
            }

            // 标题
            FlushTable(panel);
            if (line.StartsWith("### "))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = line[4..],
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
                    Margin = new Thickness(0, 4, 0, 2)
                });
                continue;
            }
            if (line.StartsWith("## "))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = line[3..],
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
                    Margin = new Thickness(0, 6, 0, 2)
                });
                continue;
            }
            if (line.StartsWith("# "))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = line[2..],
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(0, 6, 0, 2)
                });
                continue;
            }

            // 列表项
            if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
            {
                FlushTable(panel);
                var indent = line.Length - line.TrimStart().Length;
                var content = line.TrimStart()[2..];
                panel.Children.Add(CreateFormattedLine($"  • {content}", indent));
                continue;
            }

            // 引用
            if (line.TrimStart().StartsWith("> "))
            {
                FlushTable(panel);
                var quote = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                    BorderThickness = new Thickness(2, 0, 0, 0),
                    Padding = new Thickness(6, 0, 0, 0),
                    Margin = new Thickness(0, 2, 0, 2)
                };
                quote.Child = new TextBlock
                {
                    Text = line.TrimStart()[2..],
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                    FontStyle = FontStyles.Italic,
                    TextWrapping = TextWrapping.Wrap
                };
                panel.Children.Add(quote);
                continue;
            }

            // 分隔线
            FlushTable(panel);
            if (line.Trim() == "---" || line.Trim() == "***")
            {
                panel.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Height = 1,
                    Fill = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                    Margin = new Thickness(0, 4, 0, 4)
                });
                continue;
            }

            // 表格行（以 | 开头和结尾）
            if (line.Trim().StartsWith("|") && line.Trim().EndsWith("|"))
            {
                _tableBuffer.Add(line);
                continue;
            }

            // 普通文本行
            FlushTable(panel);
            panel.Children.Add(CreateFormattedLine(line));
        }

        // 刷新最后的缓冲表格
        FlushTable(panel);

        // 未关闭的代码块
        if (inCodeBlock && codeLines.Count > 0)
        {
            var codeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 2, 0, 6)
            };
            codeBorder.Child = new TextBlock
            {
                Text = string.Join("\n", codeLines),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCE, 0xCE, 0xCE)),
                FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New"),
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(codeBorder);
        }

        return panel;
    }

    /// <summary>创建带内联格式的文本行（**bold**, `code`）</summary>
    private static TextBlock CreateFormattedLine(string text, double leftMargin = 0)
    {
        var tb = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(leftMargin, 1, 0, 1)
        };

        // 解析 **bold** 和 `code`
        int pos = 0;
        while (pos < text.Length)
        {
            int boldStart = text.IndexOf("**", pos);
            int codeStart = text.IndexOf('`', pos);

            if (boldStart >= 0 && (codeStart < 0 || boldStart <= codeStart))
            {
                // 先加前面普通文本
                if (boldStart > pos)
                    tb.Inlines.Add(new Run(text[pos..boldStart]));

                int boldEnd = text.IndexOf("**", boldStart + 2);
                if (boldEnd > boldStart)
                {
                    tb.Inlines.Add(new Run(text[(boldStart + 2)..boldEnd])
                    {
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White)
                    });
                    pos = boldEnd + 2;
                }
                else { tb.Inlines.Add(new Run(text[pos..])); break; }
            }
            else if (codeStart >= 0)
            {
                if (codeStart > pos)
                    tb.Inlines.Add(new Run(text[pos..codeStart]));

                int codeEnd = text.IndexOf('`', codeStart + 1);
                if (codeEnd > codeStart)
                {
                    tb.Inlines.Add(new Run(text[(codeStart + 1)..codeEnd])
                    {
                        FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New"),
                        Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                        Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xA0, 0x60))
                    });
                    pos = codeEnd + 1;
                }
                else { tb.Inlines.Add(new Run(text[pos..])); break; }
            }
            else
            {
                tb.Inlines.Add(new Run(text[pos..]));
                break;
            }
        }

        return tb;
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { SendMessage(); e.Handled = true; }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) => SendMessage();

    private void SendMessage()
    {
        var text = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        MessageSent?.Invoke(text);
        InputBox.Clear();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();
    private void VoiceButton_Click(object sender, RoutedEventArgs e) => VoiceRequested?.Invoke();
}
