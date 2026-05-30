// 基础命名空间
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

// 解决 WPF + WinForms 类型名冲突 — 全部指向 WPF
global using Application = System.Windows.Application;
global using UserControl = System.Windows.Controls.UserControl;
global using Image = System.Windows.Controls.Image;
global using Point = System.Windows.Point;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using DragEventArgs = System.Windows.DragEventArgs;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using Button = System.Windows.Controls.Button;
global using MessageBox = System.Windows.MessageBox;
global using DataFormats = System.Windows.DataFormats;
global using DragDropEffects = System.Windows.DragDropEffects;
