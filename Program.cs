using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace PCStudio {
  internal static class Program {
    static Window window; static TextBox report; static bool mock; static string testBackupPath; static Guid mockPower = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"); static int mockAnimation = 1;
    [STAThread] static int Main(string[] args) {
      try {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml")) {
          if (s == null) throw new InvalidOperationException("Interface resource missing.");
          window = (Window)XamlReader.Load(s);
        }
        report = (TextBox)window.FindName("Report");
        ((Button)window.FindName("Refresh")).Click += delegate { Refresh(); };
        AttachLinks(window);
        ((Button)window.FindName("ApplyOptimization")).Click += delegate { ApplyOptimization(); };
        ((Button)window.FindName("RestoreOptimization")).Click += delegate { RestoreOptimization(); };
        BindRepair("SfcCheck", "sfc /verifyonly", false, "Проверка системных файлов");
        BindRepair("SfcRepair", "sfc /scannow", true, "Восстановление системных файлов");
        BindRepair("DismCheck", "DISM /Online /Cleanup-Image /ScanHealth", false, "Проверка хранилища Windows");
        BindRepair("DismRepair", "DISM /Online /Cleanup-Image /RestoreHealth", true, "Восстановление хранилища Windows");
        if (args.Length > 0 && args[0] == "--self-test") { Console.WriteLine("XAML loaded; buttons bound"); return 0; }
        if (args.Length == 2 && args[0] == "--test-optimization") {
          mock=true; testBackupPath=args[1];
          if(File.Exists(testBackupPath)) throw new InvalidOperationException("Test file already exists.");
          ((CheckBox)window.FindName("Animations")).IsChecked=true;
          ((CheckBox)window.FindName("Power")).IsChecked=true;
          ApplyOptimization();
          if(mockAnimation!=0 || mockPower!=HighPerformance || ReadBackup().Count!=2) throw new Exception("Apply test failed");
          ApplyOptimization();
          if(ReadBackup()["animations"]!="1" || new Guid(ReadBackup()["power"])!=Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e")) throw new Exception("Backup was overwritten");
          RestoreOptimization();
          if(mockAnimation!=1 || mockPower!=Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e") || ReadBackup().Count!=0) throw new Exception("Restore test failed");
          Console.WriteLine("Mock apply, repeated apply, and restore passed."); return 0;
        }
        window.Loaded += delegate { Refresh(); };
        new Application().Run(window); return 0;
      } catch (Exception ex) { if (args.Length > 0 && (args[0] == "--self-test" || args[0] == "--test-optimization")) { Console.Error.WriteLine(ex.ToString()); return 1; } MessageBox.Show("Не удалось запустить PC Studio: " + ex.Message, "PC Studio", MessageBoxButton.OK, MessageBoxImage.Error); return 1; }
    }
    [DllImport("user32.dll", SetLastError=true, EntryPoint="SystemParametersInfoW")]
    static extern bool GetAnimation(uint action, uint param, ref int value, uint flags);
    [DllImport("user32.dll", SetLastError=true, EntryPoint="SystemParametersInfoW")]
    static extern bool SetAnimation(uint action, uint param, IntPtr value, uint flags);
    [DllImport("powrprof.dll")] static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);
    [DllImport("powrprof.dll")] static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr ptr);
    static readonly Guid HighPerformance = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    static string BackupPath { get { if (mock) return testBackupPath; return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCStudio", "optimization-backup.txt"); } }
    static Dictionary<string,string> ReadBackup() {
      var values = new Dictionary<string,string>();
      if (!File.Exists(BackupPath)) return values;
      foreach (string line in File.ReadAllLines(BackupPath)) {
        int split = line.IndexOf('='); if (split <= 0) throw new InvalidDataException("Некорректная резервная запись. Настройки не изменены.");
        string key=line.Substring(0,split), val=line.Substring(split+1);
        Guid guid; if (key=="power" && !Guid.TryParse(val,out guid)) throw new InvalidDataException("Некорректный сохранённый план питания.");
        if (key=="animations" && val!="0" && val!="1") throw new InvalidDataException("Некорректное сохранённое состояние анимации.");
        if (key!="power" && key!="animations") throw new InvalidDataException("Неизвестная резервная запись.");
        values.Add(key,val);
      }
      return values;
    }
    static void WriteBackup(Dictionary<string,string> values) {
      Directory.CreateDirectory(Path.GetDirectoryName(BackupPath));
      var text=new StringBuilder(); foreach (var pair in values) text.AppendLine(pair.Key+"="+pair.Value);
      string temp=BackupPath+"."+Guid.NewGuid().ToString("N")+".tmp";
      File.WriteAllText(temp,text.ToString(),Encoding.UTF8);
      if (File.Exists(BackupPath)) File.Replace(temp,BackupPath,null); else File.Move(temp,BackupPath);
    }
    static Guid CurrentPower() { if (mock) return mockPower;
      IntPtr ptr; uint result=PowerGetActiveScheme(IntPtr.Zero,out ptr);
      if (result!=0) throw new System.ComponentModel.Win32Exception((int)result);
      if (ptr==IntPtr.Zero) throw new InvalidOperationException("Не удалось определить план питания.");
      try { return (Guid)Marshal.PtrToStructure(ptr,typeof(Guid)); } finally { LocalFree(ptr); }
    }
    static int CurrentAnimation() { if (mock) return mockAnimation;
      int value=0; if (!GetAnimation(0x1042,0,ref value,0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); return value==0?0:1;
    }
    static void ChangePower(Guid value) { if (mock) { mockPower=value; return; } uint result=PowerSetActiveScheme(IntPtr.Zero,ref value); if (result!=0) throw new System.ComponentModel.Win32Exception((int)result); }
    static void ChangeAnimation(int value) { if (mock) { mockAnimation=value; return; } if (!SetAnimation(0x1043,0,new IntPtr(value),3)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
    static void ApplyOptimization() {
      var animation=(CheckBox)window.FindName("Animations"); var power=(CheckBox)window.FindName("Power");
      var status=(TextBlock)window.FindName("OptimizationStatus"); var output=new StringBuilder();
      if (animation.IsChecked!=true && power.IsChecked!=true) { status.Text="Сначала выбери хотя бы одно действие."; return; }
      Dictionary<string,string> backup; try { backup=ReadBackup(); } catch(Exception e) { status.Text="Не удалось прочитать резервные настройки: " + e.Message; return; }
      if (animation.IsChecked==true) {
        try { if(!backup.ContainsKey("animations")) { backup.Add("animations",CurrentAnimation().ToString()); try { WriteBackup(backup); } catch { backup.Remove("animations"); throw; } } ChangeAnimation(0); output.AppendLine("Анимация интерфейса отключена."); }
        catch(Exception e) { output.AppendLine("Анимация: не удалось применить — " + e.Message); }
      }
      if (power.IsChecked==true) {
        try { if(!backup.ContainsKey("power")) { backup.Add("power",CurrentPower().ToString()); try { WriteBackup(backup); } catch { backup.Remove("power"); throw; } } ChangePower(HighPerformance); output.AppendLine("Включён план высокой производительности."); }
        catch(Exception e) { output.AppendLine("Питание: не удалось применить — " + e.Message); }
      }
      status.Text=output.ToString()+(backup.Count>0 ? "Предыдущие значения сохранены для восстановления." : "Изменения не применены.");
    }
    static void RestoreOptimization() {
      var status=(TextBlock)window.FindName("OptimizationStatus");
      try {
        var backup=ReadBackup(); if (backup.Count==0) { status.Text="Сохранённых изменений нет."; return; }
        var output=new StringBuilder();
        if (backup.ContainsKey("animations")) { try { ChangeAnimation(Int32.Parse(backup["animations"])); backup.Remove("animations"); WriteBackup(backup); output.AppendLine("Прежняя настройка анимации восстановлена."); } catch(Exception e) { output.AppendLine("Анимация: " + e.Message); } }
        if (backup.ContainsKey("power")) { try { ChangePower(new Guid(backup["power"])); backup.Remove("power"); WriteBackup(backup); output.AppendLine("Прежний план питания восстановлен."); } catch(Exception e) { output.AppendLine("Питание: " + e.Message); } }
        status.Text=output.ToString();
      } catch(Exception e) { status.Text="Не удалось восстановить настройки: " + e.Message; }
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
