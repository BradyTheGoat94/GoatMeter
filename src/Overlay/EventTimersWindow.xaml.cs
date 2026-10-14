using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Aion2DPSPro.Schedules;

namespace Aion2DPSPro.Overlay;
public partial class EventTimersWindow : Window
{
    readonly DispatcherTimer timer=new() {Interval=TimeSpan.FromSeconds(1)};
    public Func<DateTimeOffset> Clock {get;set;}=()=>DateTimeOffset.UtcNow;
    static string CustomPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aion2DPSPro","event-schedule.json");
    static string SettingsPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aion2DPSPro","event-timer-profile.txt");
    public EventTimersWindow()
    {
        InitializeComponent();timer.Tick+=(_,_)=>Refresh();
        try
        {
            Profiles.ItemsSource=EventSchedule.Load(File.Exists(CustomPath)?CustomPath:Path.Combine(AppContext.BaseDirectory,"Schedules","global-community.json"));
            var selected=File.Exists(SettingsPath)?File.ReadAllText(SettingsPath).Trim():"";
            Profiles.SelectedItem=Profiles.Items.Cast<ScheduleProfile>().FirstOrDefault(p=>p.Id==selected)??Profiles.Items[0];
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {SourceNote.Text="Unable to load schedule: "+ex.Message;}
        IsVisibleChanged+=(_,_)=>{if(IsVisible){Refresh();timer.Start();}else timer.Stop();};
        Closed+=(_,_)=>timer.Stop();
    }
    public void Refresh()
    {
        if(Profiles.SelectedItem is not ScheduleProfile p)return;
        var now=Clock();SourceNote.Text=$"{p.TimeZoneId} • Community estimate • reviewed {p.ReviewedUtc}\nSource: {p.Source}";
        EventRows.ItemsSource=EventSchedule.Evaluate(p,now).Select(w=>new TimerRow(w.Event.Name,
            w.InEntryWindow?"Scheduled entry window — closes in":w.Remaining(now)<=TimeSpan.FromMinutes(5)?"Scheduled start soon — unconfirmed":"Next scheduled opening",
            Format(w.Remaining(now)),(w.InEntryWindow?w.End:w.Start).ToLocalTime().ToString("HH:mm zzz"),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(w.Event.Id=="spacetime-rift"?"#543398":"#126B68")))).ToArray();
    }
    static string Format(TimeSpan t)=>$"{(int)Math.Max(0,t.TotalHours):00}:{Math.Max(0,t.Minutes):00}:{Math.Max(0,t.Seconds):00}";
    void ProfileChanged(object sender,SelectionChangedEventArgs e)
    {
        if(Profiles?.SelectedItem is not ScheduleProfile p)return;Refresh();
        try {Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);File.WriteAllText(SettingsPath,p.Id);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){SourceNote.Text+=" • Profile not saved";}
    }
    void ImportSchedule(object sender,RoutedEventArgs e)
    {
        var picker=new Microsoft.Win32.OpenFileDialog {Title="Import GoatMeter schedule profiles",Filter="JSON schedule|*.json"};
        if(picker.ShowDialog(this)!=true)return;
        try
        {
            var profiles=EventSchedule.Load(picker.FileName);var bytes=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(profiles,new System.Text.Json.JsonSerializerOptions {WriteIndented=true});
            Directory.CreateDirectory(Path.GetDirectoryName(CustomPath)!);File.WriteAllBytes(CustomPath+".tmp",bytes);File.Move(CustomPath+".tmp",CustomPath,true);
            Profiles.ItemsSource=profiles;Profiles.SelectedIndex=0;Refresh();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {MessageBox.Show(this,ex.Message,"Schedule not imported");}
    }
    sealed record TimerRow(string Name,string State,string Countdown,string LocalTime,Brush Background);
}
