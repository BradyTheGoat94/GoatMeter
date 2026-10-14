using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aion2DPSPro;
using Aion2DPSPro.Overlay;

internal static class Program
{
 [STAThread]
 static void Main()
 {
  var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
  var window=new OverlayWindow();
  void Invoke(string name,string value)=>typeof(OverlayWindow).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,new object[]{value});
  string? previous=null;
  foreach(var theme in new[]{"Aion Blue/Red","Neon Spectrum","Void Purple","Emerald Glass","Solar Flare","Ice Crystal","Midnight Gold","Rose Quartz","Aurora","Black / Orange","Black / Yellow","Nightmare Before Christmas"})
  {
   Invoke("ApplyTheme",theme);
   string color=((SolidColorBrush)((Border)window.FindName("Root")).Background).Color.ToString();
   if(color==previous)throw new Exception($"Theme did not change: {theme}");previous=color;
  }
  foreach(var style in new[]{"Pulse Cards","Neon Compact","Encounter Focus","Modern Glass","Details Inspired","Kagerou Inspired","Bars Only","Raid Compact","Glass Cards","Tournament","Classic Dashboard"})Invoke("ApplyOverlayStyle",style);
  if(((FrameworkElement)window.FindName("EncounterSummary")).Visibility!=Visibility.Collapsed)throw new Exception("Classic did not hide encounter header");
  if(((FrameworkElement)window.FindName("ToolbarGrid")).Visibility!=Visibility.Visible)throw new Exception("Classic did not restore toolbar");
  var t=DateTime.UtcNow;var engine=new CombatEngine(()=>t);
  engine.Apply(new(t,CombatKind.PlayerName,1,"Player",SourceClass:"Templar"));
  engine.Apply(new(t,CombatKind.Damage,1,"Player",2,"Target","Strike",100,SourceClass:"Templar"));
  window.SnapshotProvider=(segment,category)=>engine.Snapshot(segment,category);
  var tabs=(TabControl)window.FindName("Tabs");var segment=(ComboBox)window.FindName("Segment");
  for(int s=0;s<3;s++)for(int c=0;c<8;c++){segment.SelectedIndex=s;tabs.SelectedIndex=c;window.Render(engine.Snapshot());}
  tabs.SelectedIndex=0;segment.SelectedIndex=0;window.Render(engine.Snapshot());
  if(((ListView)window.FindName("Rows")).Items.Count!=1)throw new Exception("Damage rows missing");
  // Exercise the actual header action from Overall; reset must select Current,
  // clear rows immediately, archive once and leave session totals untouched.
  window.Engine=engine; segment.SelectedIndex=2;
  ((Button)window.FindName("ResetCurrentFight")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  if(segment.SelectedIndex!=0 || ((ListView)window.FindName("Rows")).Items.Count!=0)throw new Exception("Reset did not return to empty current fight");
  if(engine.History.Count!=1 || engine.Snapshot(MeterSegment.Previous,MeterCategory.Damage).FightDamage!=100)throw new Exception("Reset lost completed fight");
  if(engine.Snapshot(MeterSegment.Overall,MeterCategory.Damage).FightDamage!=100)throw new Exception("Reset cleared overall totals");
  ((Button)window.FindName("ResetCurrentFight")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  if(engine.History.Count!=1)throw new Exception("Empty reset duplicated history");
  engine.Apply(new(t.AddSeconds(1),CombatKind.Damage,1,"Player",2,"Target","Strike",50,SourceClass:"Templar"));
  window.Render(engine.Snapshot());
  if(engine.Snapshot().FightDamage!=50 || engine.Snapshot(MeterSegment.Overall,MeterCategory.Damage).FightDamage!=150)throw new Exception("Capture did not continue after reset");
  Console.WriteLine("PASS: header reset archives current, preserves overall and capture continues");
  tabs.SelectedIndex=1;window.Render(engine.Snapshot());
  if(((ListView)window.FindName("Rows")).Items.Count!=0)throw new Exception("Healing incorrectly reused damage rows");
  // Integration: identity-only socket and combat socket, same exact entity and endpoint scope.
  var captureEngine=new CombatEngine(()=>t);
  var identityProfile=new Aion2DPSPro.Protocol.ProtocolProfile("test","test","test",13328,new Dictionary<string,Aion2DPSPro.Protocol.PacketTag>{{"selfInfo",new(51,54)},{"damage",new(4,56)}});
  using(var adapter=new Aion2DPSPro.Capture.LiveCaptureAdapter(new Aion2DPSPro.Protocol.CurrentClientDecoder(identityProfile)))
  {
   adapter.EventReceived+=captureEngine.Apply;
   var process=typeof(Aion2DPSPro.Capture.LiveCaptureAdapter).GetMethod("Process",BindingFlags.NonPublic|BindingFlags.Instance)!;
   void Packet(ushort localPort,string hex)
   {
    var ip=new PacketDotNet.IPv4Packet(System.Net.IPAddress.Parse("10.0.0.2"),System.Net.IPAddress.Parse("10.0.0.1"));
    var tcp=new PacketDotNet.TcpPacket(13328,localPort) {SequenceNumber=100,PayloadData=Convert.FromHexString(hex)};
    ip.PayloadPacket=tcp;process.Invoke(adapter,new object[]{"test-adapter",ip,t});
   }
   Packet(50001,"193336FD235F81C1283708546573744865726F000000");
   Packet(50002,"210438E3A0020400FD2340B7B70009020B95C34701000000D658E7020100");
   if(captureEngine.Snapshot().Players.Single().Name!="TestHero")throw new Exception("Identity-only connection did not resolve combat name");
   // The latest captured self ID changes after reconnect; never carry a name across generations.
   t=t.AddSeconds(31);
   Packet(50003,"193336AD5F5F91C1283708546573744865726F000000");
   Packet(50004,"240438E3A0020600AD5F40B7B70002020000020B95C34701000000AC5881030100");
   if(!captureEngine.Snapshot().Players.Any(p=>p.EntityId==12205&&p.Name=="TestHero"))throw new Exception("Reconnect identity did not resolve new self entity");
   adapter.Dispose();adapter.Completion.GetAwaiter().GetResult();
  }
  Console.WriteLine("PASS: separate identity/combat sockets resolve exact actor name");
  var reportEngine=new CombatEngine(()=>t.AddSeconds(15));
  reportEngine.Apply(new(t,CombatKind.PlayerName,1,"TestHero",SourceClass:"Templar"));
  reportEngine.Apply(new(t,CombatKind.Damage,1,"TestHero",2,"Training Scarecrow","Punishing Strike",18000,SourceClass:"Templar",DamageFlags:DamageFlags.Critical|DamageFlags.Perfect));
  reportEngine.Apply(new(t.AddSeconds(4),CombatKind.Damage,1,"TestHero",2,"Training Scarecrow","Desperate Strike",12000,SourceClass:"Templar",DamageFlags:DamageFlags.Back));
  reportEngine.Apply(new(t.AddSeconds(8),CombatKind.Damage,1,"TestHero",2,"Training Scarecrow","Pummel",9000,SourceClass:"Templar"));
  reportEngine.Apply(new(t.AddSeconds(10),CombatKind.Heal,1,"TestHero",1,"TestHero","Mend",3500,SourceClass:"Templar"));
  var report=new CombatReportWindow(()=>reportEngine.Snapshot(MeterSegment.Current,MeterCategory.Damage,true),1);
  report.Show();report.UpdateLayout();
  if(((DataGrid)report.FindName("SkillGrid")).Items.Count!=3)throw new Exception("Report damage skills missing");
  if(((DataGrid)report.FindName("TargetGrid")).Items.Count!=1)throw new Exception("Report targets missing");
  var reportCategories=(ComboBox)report.FindName("Category");reportCategories.SelectedItem=MeterCategory.Healing;report.Refresh();
  if(((DataGrid)report.FindName("SkillGrid")).Items.Count!=1)throw new Exception("Report healing skills missing");
  reportEngine.Apply(new(t.AddSeconds(11),CombatKind.Heal,1,"TestHero",1,"TestHero","Second heal",500));report.Refresh();
  if(((DataGrid)report.FindName("SkillGrid")).Items.Count!=2)throw new Exception("Report live refresh missing");
  ((CheckBox)report.FindName("Pause")).IsChecked=true;
  reportEngine.Apply(new(t.AddSeconds(12),CombatKind.Heal,1,"TestHero",1,"TestHero","Third heal",500));report.Refresh(true);
  if(((DataGrid)report.FindName("SkillGrid")).Items.Count!=2)throw new Exception("Report pause changed data");
  ((CheckBox)report.FindName("Pause")).IsChecked=false;reportCategories.SelectedItem=MeterCategory.Damage;report.Refresh();report.UpdateLayout();
  var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)report.ActualWidth,(int)report.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(report);
  var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
  using(var file=System.IO.File.Create("report-preview.png"))encoder.Save(file);
  report.Close();
  Console.WriteLine("PASS: modern report data, categories, targets, live refresh and pause");
  window.SnapshotProvider=null;
  var previewEngine=new CombatEngine(()=>t.AddSeconds(25)) {PreviewMode=true,InactivityTimeout=TimeSpan.FromSeconds(60),BossInactivityTimeout=TimeSpan.FromSeconds(60)};
  string[] classes={"Templar","Sorcerer","Assassin","Ranger","Spiritmaster","Gladiator","Cleric","Chanter"};
  for(int i=0;i<classes.Length;i++) {
   string playerName=i==0?"TestHero":"Party member "+(i+1);
   previewEngine.Apply(new(t,CombatKind.PlayerName,i+100,playerName,SourceClass:classes[i],SourceServerId:1102,SourceCharacterId:i+100000,SourceGearScore:2800+i*35,SourceCombatPower:121600-i*8500,StatsObservedUtc:t));
   previewEngine.Apply(new(t,CombatKind.Damage,i+100,playerName,900,"Training target","Opening skill",(8-i)*5000,SourceClass:classes[i]));
   previewEngine.Apply(new(t.AddSeconds(25),CombatKind.Damage,i+100,playerName,900,"Training target","Finishing skill",(8-i)*3500,SourceClass:classes[i]));
  }
  tabs.SelectedIndex=0;segment.SelectedIndex=0;Invoke("ApplyTheme","Ice Crystal");window.Show();
  var rows=(ListView)window.FindName("Rows");
  DataTemplate? previousTemplate=null;
  foreach(var style in new[]{"Pulse Cards","Neon Compact","Encounter Focus","Modern Glass","Details Inspired","Kagerou Inspired","Classic Dashboard"}) {
   Invoke("ApplyOverlayStyle",style);Invoke("ApplyTheme",style is "Modern Glass" or "Encounter Focus" or "Pulse Cards" or "Neon Compact"?"Aion Blue/Red":"Ice Crystal");window.Render(previewEngine.Snapshot());window.UpdateLayout();
   if(rows.ItemTemplate==previousTemplate)throw new Exception("Styles reused the same row design");previousTemplate=rows.ItemTemplate;
   if(rows.Items.Count!=8)throw new Exception("Style lost party rows");
   if(!rows.Items.Cast<object>().All(r=>r.GetType().GetProperty("CharacterStats")!.GetValue(r)!.ToString()!.Contains("GS")))throw new Exception("Preset lost universal GS/CP metadata");
   rows.SelectedIndex=0;
   var overlayBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);overlayBitmap.Render(window);
   var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(overlayBitmap));
   using(var output=System.IO.File.Create("overlay-"+style.Replace(" ","-")+".png"))png.Save(output);
  }
  // Render both requested layouts in both black themes from the actual WPF tree.
  foreach(var style in new[]{"Encounter Focus","Modern Glass"})
  {
   Invoke("ApplyOverlayStyle",style);window.Render(previewEngine.Snapshot());window.UpdateLayout();
   string[] originalClassColors=rows.Items.Cast<object>().Select(r=>((SolidColorBrush)r.GetType().GetProperty("Brush")!.GetValue(r)!).Color.ToString()).ToArray();
   foreach(var theme in style=="Encounter Focus"?new[]{"Black / Orange","Black / Yellow","Nightmare Before Christmas"}:new[]{"Black / Orange","Black / Yellow"})
   {
    Invoke("ApplyTheme",theme);window.Render(previewEngine.Snapshot());window.UpdateLayout();
    var actualClassColors=rows.Items.Cast<object>().Select(r=>((SolidColorBrush)r.GetType().GetProperty("Brush")!.GetValue(r)!).Color.ToString()).ToArray();
    if(!originalClassColors.SequenceEqual(actualClassColors))throw new Exception("Black theme changed class colors");
    var rootColor=((SolidColorBrush)((Border)window.FindName("Root")).Background).Color;
    if(rootColor.R!=8||rootColor.G!=8||rootColor.B!=8)throw new Exception("Black theme did not retain neutral black surface");
    var expectedAccent=(Color)ColorConverter.ConvertFromString(theme=="Black / Orange"?"#FF8A00":theme=="Black / Yellow"?"#FFD400":"#EEEBDD");
    if(((SolidColorBrush)((Border)window.FindName("Root")).BorderBrush).Color!=expectedAccent)throw new Exception("Black theme accent incorrect");
    // Inspect the visible row progress bars as well as the row model brushes.
    for(int i=0;i<rows.Items.Count;i++)
    {
     if(rows.ItemContainerGenerator.ContainerFromIndex(i) is not DependencyObject container)continue;
     var pending=new Queue<DependencyObject>();pending.Enqueue(container);
     while(pending.Count>0)
     {
      var child=pending.Dequeue();
      if(child is ProgressBar bar && ((SolidColorBrush)bar.Foreground).Color.ToString()!=originalClassColors[i])throw new Exception("Theme recolored class damage bar");
      for(int c=0;c<VisualTreeHelper.GetChildrenCount(child);c++)pending.Enqueue(VisualTreeHelper.GetChild(child,c));
     }
    }
    var themedBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);themedBitmap.Render(window);
    var encoderTheme=new System.Windows.Media.Imaging.PngBitmapEncoder();encoderTheme.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
    if(theme=="Nightmare Before Christmas" && ((Image)window.FindName("NightmarePortrait")).Visibility!=Visibility.Visible)throw new Exception("Nightmare portrait missing");
    string suffix=theme=="Black / Orange"?"Black-Orange":theme=="Black / Yellow"?"Black-Yellow":"Nightmare-Before-Christmas";
    using(var output=System.IO.File.Create("overlay-"+style.Replace(" ","-")+"-"+suffix+".png"))encoderTheme.Save(output);
   }
  }
  Invoke("ApplyTheme","Aion Blue/Red");
  if(((Image)window.FindName("NightmarePortrait")).Visibility!=Visibility.Collapsed)throw new Exception("Nightmare decoration leaked into another theme");
  Console.WriteLine("PASS: black/orange and black/yellow chrome preserves class text and bars");
  Invoke("ApplyTheme","Aion Blue/Red");
  Invoke("ApplyOverlayStyle","Encounter Focus");window.Render(previewEngine.Snapshot());window.UpdateLayout();
  var lockButton=(Button)window.FindName("PositionLock");
  lockButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  if(window.ResizeMode!=ResizeMode.NoResize || lockButton.Content.ToString()!="Unlock position")throw new Exception("Position lock did not lock resizing");
  lockButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  if(window.ResizeMode!=ResizeMode.CanResizeWithGrip)throw new Exception("Position lock did not unlock");
  var quickOpacity=(Slider)window.FindName("QuickOpacity");quickOpacity.Value=.65;
  if(Math.Abs(window.Opacity-.65)>.001)throw new Exception("Quick opacity did not update overlay");
  quickOpacity.Value=1;
  ((ComboBox)window.FindName("CategoryPicker")).SelectedIndex=1;
  window.Render(previewEngine.Snapshot(MeterSegment.Current,MeterCategory.Healing));
  if(((TextBlock)window.FindName("EncounterRateLabel")).Text!="HPS" || ((TextBlock)window.FindName("EncounterTotalLabel")).Text!="HEALING")throw new Exception("Encounter columns did not follow category");
  if(((FrameworkElement)window.FindName("EncounterHp")).Visibility!=Visibility.Collapsed)throw new Exception("Unknown target HP should be hidden");
  if(((FrameworkElement)window.FindName("BossHp")).Visibility!=Visibility.Collapsed || ((TextBlock)window.FindName("TargetHp")).Text!="HP unavailable")throw new Exception("Unknown HP falsely displayed zero");
  Console.WriteLine("PASS: encounter focus columns, position lock, quick opacity and unknown HP");
  Invoke("ApplyOverlayStyle","Kagerou Inspired");
  if(((FrameworkElement)window.FindName("EncounterControls")).Visibility!=Visibility.Collapsed)throw new Exception("Preset switch left encounter controls visible");
  var picker=(ComboBox)window.FindName("CategoryPicker");picker.SelectedIndex=1;
  if(tabs.SelectedIndex!=1)throw new Exception("Compact category picker did not select healing");
  window.Render(previewEngine.Snapshot(MeterSegment.Current,MeterCategory.Healing));
  if(rows.Items.Count!=0)throw new Exception("Compact healing view reused damage");
  Console.WriteLine("PASS: distinct responsive meter designs, party rows and compact category selection");
  // All presets retain fight selectors, target information and countdown access.
  foreach(var style in new[]{"Pulse Cards","Neon Compact","Encounter Focus","Modern Glass","Details Inspired","Kagerou Inspired","Bars Only","Raid Compact","Glass Cards","Tournament","Classic Dashboard"})
  {
   Invoke("ApplyOverlayStyle",style);window.Render(previewEngine.Snapshot());window.UpdateLayout();
   if(((FrameworkElement)window.FindName("ToolbarGrid")).Visibility!=Visibility.Visible)throw new Exception("Preset lost fight/history selection: "+style);
   if(style!="Encounter Focus"&&((FrameworkElement)window.FindName("FooterBar")).Visibility!=Visibility.Visible)throw new Exception("Preset lost target information: "+style);
   if(((Button)window.FindName("CopySummary")).Visibility!=Visibility.Visible || ((Button)window.FindName("FreezeView")).Visibility!=Visibility.Visible)throw new Exception("Preset lost shared summary/freeze controls");
   if(((Button)window.FindName("ResetCurrentFight")).Visibility!=Visibility.Visible)throw new Exception("Preset lost reset");
  }
  typeof(OverlayWindow).GetField("hideNames",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,true);tabs.SelectedIndex=0;window.Render(previewEngine.Snapshot());
  if(rows.Items.Cast<object>().Any(r=>r.GetType().GetProperty("Name")!.GetValue(r)!.ToString()!.Contains("TestHero")))throw new Exception("Stream-safe names exposed actor name");
  typeof(OverlayWindow).GetField("showCharacterStats",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,false);window.Render(previewEngine.Snapshot());
  if(rows.Items.Cast<object>().Any(r=>r.GetType().GetProperty("CharacterStats")!.GetValue(r)!.ToString()!.Contains("GS")))throw new Exception("Character stats toggle ignored");
  Console.WriteLine("PASS: shared controls/info across all presets, stream-safe names, GS/CP toggle");
  var freezeEngine=new CombatEngine(()=>t);freezeEngine.Apply(new(t,CombatKind.PlayerName,70,"FreezeTester"));freezeEngine.Apply(new(t,CombatKind.Damage,70,"FreezeTester",90,"Dummy","Strike",100));
  tabs.SelectedIndex=0;segment.SelectedIndex=0;window.Engine=freezeEngine;window.SnapshotProvider=(seg,cat)=>freezeEngine.Snapshot(seg,cat);window.Render(freezeEngine.Snapshot());
  var frozenRows=rows.ItemsSource;((Button)window.FindName("FreezeView")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  freezeEngine.Apply(new(t,CombatKind.Damage,70,"FreezeTester",90,"Dummy","Strike",300));window.Render(freezeEngine.Snapshot());
  if(rows.ItemsSource!=frozenRows || freezeEngine.Snapshot().FightDamage!=400)throw new Exception("Freeze paused capture or changed display");
  ((Button)window.FindName("FreezeView")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  if(rows.ItemsSource==frozenRows)throw new Exception("Resume did not refresh capture");
  ((Button)window.FindName("FreezeView")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));segment.SelectedIndex=2;
  if(((Button)window.FindName("FreezeView")).Content.ToString()!="Ⅱ")throw new Exception("Changing segments left a misleading frozen view");
  var originalWidth=window.Width;typeof(OverlayWindow).GetMethod("SetUiScale",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,new object[]{.8});
  if(Math.Abs(window.Width-originalWidth*.8)>.01)throw new Exception("UI scale did not resize coherently");
  typeof(OverlayWindow).GetMethod("SetUiScale",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,new object[]{1.0});
  Console.WriteLine("PASS: freeze preserves capture, navigation resumes display and UI scale");
  var eventWindow=new EventTimersWindow {Owner=window,Clock=()=>new DateTimeOffset(2026,10,14,2,59,0,TimeSpan.Zero)};
  eventWindow.Show();eventWindow.Refresh();eventWindow.UpdateLayout();
  var eventItems=(ItemsControl)eventWindow.FindName("EventRows");
  if(eventItems.Items.Count!=2)throw new Exception("Imported rift/festival timers missing");
  if(!((TextBlock)eventWindow.FindName("SourceNote")).Text.Contains("Community estimate"))throw new Exception("Schedule uncertainty label missing");
  var eventBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)eventWindow.ActualWidth,(int)eventWindow.ActualHeight,96,96,PixelFormats.Pbgra32);eventBitmap.Render(eventWindow);
  var eventPng=new System.Windows.Media.Imaging.PngBitmapEncoder();eventPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(eventBitmap));
  using(var output=System.IO.File.Create("overlay-Event-Countdowns.png"))eventPng.Save(output);
  eventWindow.Close();Console.WriteLine("PASS: imported event countdown window");
  window.Close();Console.WriteLine("PASS: WPF themes, styles, segment/category switching");app.Shutdown();
 }
}
