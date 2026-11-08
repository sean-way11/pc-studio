using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace PCStudio {
  internal static class Program {
    static Window window; static TextBox report;
    [STAThread] static int Main(string[] args) {
      try {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml")) {
          if (s == null) throw new InvalidOperationException("Interface resource missing.");
          window = (Window)XamlReader.Load(s);
        }
        report = (TextBox)window.FindName("Report");
        ((Button)window.FindName("Refresh")).Click += delegate { Refresh(); };
        AttachLinks(window);
        BindRepair("SfcCheck", "sfc /verifyonly", false, "Проверка системных файлов");
        BindRepair("SfcRepair", "sfc /scannow", true, "Восстановление системных файлов");
        BindRepair("DismCheck", "DISM /Online /Cleanup-Image /ScanHealth", false, "Проверка хранилища Windows");
        BindRepair("DismRepair", "DISM /Online /Cleanup-Image /RestoreHealth", true, "Восстановление хранилища Windows");
        if (args.Length > 0 && args[0] == "--self-test") { Console.WriteLine("XAML loaded; buttons bound"); return 0; }
        window.Loaded += delegate { Refresh(); };
        new Application().Run(window); return 0;
      } catch (Exception ex) { if (args.Length > 0 && args[0] == "--self-test") { Console.Error.WriteLine(ex.ToString()); return 1; } MessageBox.Show("Не удалось запустить PC Studio: " + ex.Message, "PC Studio", MessageBoxButton.OK, MessageBoxImage.Error); return 1; }
    }
    static void AttachLinks(DependencyObject parent) {
      Button b = parent as Button;
      if (b != null && b.Tag is string) b.Click += delegate { try { Process.Start(new ProcessStartInfo((string)b.Tag) { UseShellExecute = true }); } catch (Exception e) { MessageBox.Show("Не удалось открыть настройку: " + e.Message, "PC Studio"); } };
      foreach (object child in LogicalTreeHelper.GetChildren(parent)) { DependencyObject d = child as DependencyObject; if (d != null) AttachLinks(d); }
    }
    static void BindRepair(string name, string command, bool modifies, string title) {
      Button b = (Button)window.FindName(name); if (b == null) throw new InvalidOperationException("Missing button: " + name);
      b.Click += delegate {
        string desc = modifies ? "Это средство может изменить системные файлы Windows. DISM может использовать интернет. Результат зависит от состояния системы." : "Проверка может занять продолжительное время. Результат появится в отдельном окне.";
        if (MessageBox.Show(desc + "\n\nЗапустить: " + command + "?\nПотребуется подтверждение администратора Windows.", title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try {
          string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
          ProcessStartInfo p = new ProcessStartInfo(Path.Combine(system, "cmd.exe"));
          p.UseShellExecute = true; p.Verb = "runas";
          p.Arguments = "/v:on /k \"" + command + " & echo. & echo Exit code: !ERRORLEVEL!\"";
          Process.Start(p);
        } catch (System.ComponentModel.Win32Exception e) { if (e.NativeErrorCode != 1223) MessageBox.Show("Не удалось запустить средство: " + e.Message, "PC Studio"); }
        catch (Exception e) { MessageBox.Show("Не удалось запустить средство: " + e.Message, "PC Studio"); }
      };
    }
    static void Refresh() {
      var text = new StringBuilder(); text.AppendLine("ПРОВЕРКА  ·  " + DateTime.Now.ToString("dd.MM.yyyy HH:mm")); text.AppendLine();
      try { foreach (ManagementObject o in new ManagementObjectSearcher("SELECT Caption,BuildNumber,TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem").Get()) { double total=Convert.ToDouble(o["TotalVisibleMemorySize"]), free=Convert.ToDouble(o["FreePhysicalMemory"]); double used=100*(total-free)/total; text.AppendLine("Windows: " + o["Caption"] + "  ·  сборка " + o["BuildNumber"]); text.AppendLine(String.Format("Оперативная память: занято {0:0}%  ·  свободно {1:0.0} из {2:0.0} ГБ",used,free/1048576,total/1048576)); if (used>=85) text.AppendLine("Совет: проверь работающие приложения в диспетчере задач."); } } catch { text.AppendLine("Данные о системе и памяти недоступны."); }
      text.AppendLine();
      try { int count=0, sum=0; foreach (ManagementObject o in new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor").Get()) { if (o["LoadPercentage"]!=null) {sum+=Convert.ToInt32(o["LoadPercentage"]); count++;} } if (count>0) { int load=sum/count; text.AppendLine("Загрузка процессора: около " + load + "%"); if(load>=80) text.AppendLine("Совет: если нагрузка держится долго, проверь процессы."); } else text.AppendLine("Загрузка процессора недоступна."); } catch { text.AppendLine("Данные о процессоре недоступны."); }
      text.AppendLine();
      try { foreach (DriveInfo d in DriveInfo.GetDrives()) if (d.DriveType==DriveType.Fixed && d.IsReady) { double free=d.AvailableFreeSpace/1073741824.0, total=d.TotalSize/1073741824.0; text.AppendLine(String.Format("Диск {0}: свободно {1:0.0} из {2:0.0} ГБ",d.Name,free,total)); if(free/total<.15) text.AppendLine("Совет: проверь, что занимает место, в настройках памяти."); } } catch { text.AppendLine("Некоторые данные о дисках недоступны."); }
      text.AppendLine(); text.AppendLine("Показатели меняются во время работы. Проверка не является тестом оборудования."); report.Text=text.ToString();
    }
  }
}
