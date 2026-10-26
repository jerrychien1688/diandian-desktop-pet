using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Interop;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

namespace DiandianDesktop {
 public static class Program {
  [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
  [STAThread] public static int Main(string[] args) {
   bool test=args.Length>0&&args[0]=="--self-test";Mutex mutex=null;
   try {
    SetProcessDPIAware();bool fresh=true;if(!test){mutex=new Mutex(true,"Local\\DiandianDesktopPet_v1",out fresh);if(!fresh){MessageBox.Show("點點已在執行。請從工作列右下角的點點圖示選擇「顯示點點」。","點點桌寵");return 0;}}
    Application app=new Application();app.ShutdownMode=ShutdownMode.OnMainWindowClose;
    PetWindow pet=new PetWindow(test);app.MainWindow=pet;
    if(test){string report=args.Length>1?Path.GetFullPath(args[1]):Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification");pet.Opacity=0;pet.Loaded+=delegate {pet.Dispatcher.BeginInvoke(new Action(delegate{try{pet.RunChecks(report);app.Shutdown(0);}catch(Exception e){Directory.CreateDirectory(report);File.WriteAllText(Path.Combine(report,"FAILED.txt"),e.ToString());app.Shutdown(2);}}),DispatcherPriority.ApplicationIdle);};}
    return app.Run(pet);
   } catch(Exception e){if(test){Console.Error.WriteLine(e);return 2;}MessageBox.Show("點點暫時無法啟動：\n"+e.Message,"點點桌寵");return 1;}
   finally{if(mutex!=null)mutex.Dispose();}
  }
 }
 public class AlphaImage:Image {
  byte[] pixels;int pw,ph;
  public void SetBitmap(BitmapSource bitmap){Source=bitmap;pw=bitmap.PixelWidth;ph=bitmap.PixelHeight;FormatConvertedBitmap converted=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);pixels=new byte[pw*ph*4];converted.CopyPixels(pixels,pw*4,0);}
  protected override HitTestResult HitTestCore(PointHitTestParameters p){Point q=p.HitPoint;if(pixels==null||ActualWidth<=0||ActualHeight<=0)return null;int x=(int)(q.X*pw/ActualWidth),y=(int)(q.Y*ph/ActualHeight);if(x<0||y<0||x>=pw||y>=ph||pixels[(y*pw+x)*4+3]<20)return null;return base.HitTestCore(p);}
 }
 public class PetWindow:Window {
  const string Version="1.0";
  readonly bool testing;readonly Canvas stage=new Canvas();readonly AlphaImage cat=new AlphaImage();readonly TextBlock heart=new TextBlock();readonly ScaleTransform breathing=new ScaleTransform(1,1);readonly Dictionary<string,BitmapSource> poses=new Dictionary<string,BitmapSource>();readonly DispatcherTimer timer=new DispatcherTimer();readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();readonly string baseDir=AppDomain.CurrentDomain.BaseDirectory;readonly List<MenuItem> poseItems=new List<MenuItem>();
  Forms.NotifyIcon tray;ContextMenu menu;MenuItem autoItem,motionItem,topItem;string pose="sit";double scale=1,phaseStart,heartUntil,dragX,dragY,leftDown,topDown;bool autoPose=true,motion=true,dragging,moved,ready;double dpi=1;double nextPose=55;string settingsPath;int frames;
  [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
  public PetWindow(bool isTest){testing=isTest;Title="點點桌寵";WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=true;UseLayoutRounding=true;SnapsToDevicePixels=false;WindowStartupLocation=WindowStartupLocation.Manual;
   using(Drawing.Graphics g=Drawing.Graphics.FromHwnd(IntPtr.Zero))dpi=g.DpiX/96.0;
   settingsPath=Path.Combine(baseDir,"Settings.ini");LoadPose("sit");LoadPose("relax");LoadPose("sleep");
   stage.ClipToBounds=false;Content=stage;cat.Stretch=Stretch.Fill;cat.RenderTransform=breathing;cat.RenderTransformOrigin=new Point(.5,1);RenderOptions.SetBitmapScalingMode(cat,BitmapScalingMode.HighQuality);stage.Children.Add(cat);
   heart.Text="♥";heart.FontSize=26;heart.Foreground=new SolidColorBrush(Color.FromRgb(227,128,147));heart.IsHitTestVisible=false;heart.Opacity=0;stage.Children.Add(heart);
   BuildMenu();cat.ContextMenu=menu;cat.ToolTip="點點 · 拖曳移動 / 點一下摸摸 / 連點兩下換姿勢 / 右鍵選單";
   cat.MouseLeftButtonDown+=OnDown;cat.MouseMove+=OnMove;cat.MouseLeftButtonUp+=OnUp;cat.LostMouseCapture+=delegate{dragging=false;};cat.MouseWheel+=delegate(object sender,MouseWheelEventArgs e){ResizePet(scale+(e.Delta>0?.1:-.1));e.Handled=true;};
   Loaded+=delegate{ready=true;ReadSettings();double savedTop=Top;LayoutPet();if(!double.IsNaN(savedTop))Top=savedTop;if(double.IsNaN(Left)||double.IsNaN(Top)||!File.Exists(settingsPath)||testing)ResetPosition();ClampPosition();if(!testing)BuildTray();timer.Start();};
   Closing+=delegate{if(!testing)SaveSettings();timer.Stop();if(tray!=null){tray.Visible=false;tray.Dispose();}};
   timer.Interval=TimeSpan.FromMilliseconds(40);timer.Tick+=Tick;LayoutPet();
  }
  void LoadPose(string name){using(Stream st=Assembly.GetExecutingAssembly().GetManifestResourceStream("assets."+name+".png")){if(st==null)throw new InvalidDataException("缺少點點素材："+name);BitmapImage b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;b.StreamSource=st;b.EndInit();b.Freeze();poses[name]=b;}}
  static MenuItem Item(string text,Action action){MenuItem m=new MenuItem();m.Header=text;m.Click+=delegate{action();};return m;}
  void BuildMenu(){menu=new ContextMenu();MenuItem title=new MenuItem{Header="點點 · 真實照片風格",IsEnabled=false};menu.Items.Add(title);menu.Items.Add(new Separator());string[] ids={"sit","relax","sleep"},names={"坐著陪你","趴著休息","睡個覺"};for(int i=0;i<ids.Length;i++){string key=ids[i];MenuItem m=Item(names[i],delegate{autoPose=false;SetPose(key);SyncMenus();SaveSettings();});m.IsCheckable=true;m.Tag=key;poseItems.Add(m);menu.Items.Add(m);}
   autoItem=Item("自動換姿勢",delegate{autoPose=!autoPose;nextPose=clock.Elapsed.TotalSeconds+55;SyncMenus();SaveSettings();});autoItem.IsCheckable=true;menu.Items.Add(autoItem);
   motionItem=Item("輕微呼吸效果",delegate{motion=!motion;SyncMenus();SaveSettings();});motionItem.IsCheckable=true;menu.Items.Add(motionItem);
   MenuItem sizes=new MenuItem{Header="大小"};double[] values={.65,.85,1,1.25,1.5};string[] labels={"迷你 65%","小 85%","標準 100%","大 125%","特大 150%"};for(int i=0;i<values.Length;i++){double val=values[i];sizes.Items.Add(Item(labels[i],delegate{ResizePet(val);}));}menu.Items.Add(sizes);
   topItem=Item("保持在最上層",delegate{Topmost=!Topmost;SyncMenus();SaveSettings();});topItem.IsCheckable=true;menu.Items.Add(topItem);menu.Items.Add(Item("回到螢幕右下角",ResetPosition));menu.Items.Add(Item("暫時隱藏（從右下角圖示叫回）",delegate{Hide();}));menu.Items.Add(new Separator());menu.Items.Add(Item("使用說明",ShowHelp));menu.Items.Add(Item("結束點點桌寵",delegate{Close();}));menu.Opened+=delegate{SyncMenus();};SyncMenus();
  }
  void SyncMenus(){autoItem.IsChecked=autoPose;motionItem.IsChecked=motion;topItem.IsChecked=Topmost;foreach(MenuItem m in poseItems)m.IsChecked=(string)m.Tag==pose;}
  void BuildTray(){tray=new Forms.NotifyIcon();using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream("assets.sit.png"))using(Drawing.Bitmap source=new Drawing.Bitmap(s))using(Drawing.Bitmap tiny=new Drawing.Bitmap(48,48)){using(Drawing.Graphics g=Drawing.Graphics.FromImage(tiny)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(source,new Drawing.Rectangle(4,0,40,48));}IntPtr h=tiny.GetHicon();using(Drawing.Icon ic=Drawing.Icon.FromHandle(h))tray.Icon=(Drawing.Icon)ic.Clone();DestroyIcon(h);}
   tray.Text="點點桌寵 · 右鍵選單";Forms.ContextMenuStrip tm=new Forms.ContextMenuStrip();tm.Items.Add("顯示點點",null,delegate{Dispatcher.Invoke(new Action(delegate{Show();ClampPosition();}));});tm.Items.Add("回到右下角",null,delegate{Dispatcher.Invoke(new Action(delegate{Show();ResetPosition();}));});tm.Items.Add("姿勢與大小設定",null,delegate{Dispatcher.Invoke(new Action(delegate{Show();menu.PlacementTarget=cat;menu.IsOpen=true;}));});tm.Items.Add(new Forms.ToolStripSeparator());tm.Items.Add("結束",null,delegate{Dispatcher.Invoke(new Action(Close));});tray.ContextMenuStrip=tm;tray.DoubleClick+=delegate{Dispatcher.Invoke(new Action(delegate{Show();ClampPosition();}));};tray.Visible=true;
  }
  void LayoutPet(){double oldBottom=double.IsNaN(Top)?0:Top+Height;Width=420*scale;Height=360*scale;BitmapSource b=poses[pose];cat.Width=Width;cat.Height=Height;cat.SetBitmap(b);Canvas.SetLeft(cat,0);Canvas.SetTop(cat,0);Canvas.SetLeft(heart,Width/2-12);Canvas.SetTop(heart,Math.Max(0,Height-PoseHeight()-52*scale));if(ready&&oldBottom>0)Top=oldBottom-Height;SyncMenus();}
  double PoseHeight(){return (pose=="sit"?316:pose=="relax"?245:235)*scale;}
  void SetPose(string next){if(!poses.ContainsKey(next))throw new ArgumentException("Unknown pose");pose=next;LayoutPet();phaseStart=clock.Elapsed.TotalSeconds;nextPose=phaseStart+(pose=="sleep"?90:55);if(!testing){DoubleAnimation a=new DoubleAnimation(.65,1,TimeSpan.FromMilliseconds(240));cat.BeginAnimation(OpacityProperty,a);}ClampPosition();}
  void ResizePet(double value){double center=double.IsNaN(Left)?0:Left+Width/2;scale=Math.Max(.5,Math.Min(1.6,value));LayoutPet();if(center>0)Left=center-Width/2;ClampPosition();SaveSettings();}
  void Tick(object sender,EventArgs e){frames++;double t=clock.Elapsed.TotalSeconds;if(!IsVisible||dragging||menu.IsOpen)return;if(autoPose&&t>=nextPose)SetPose(pose=="sit"?"relax":pose=="relax"?"sleep":"sit");double amount=motion?(pose=="sleep"?.006:.0035):0;breathing.ScaleY=1+amount*Math.Sin((t-phaseStart)*(pose=="sleep"?1.3:1.7));heart.Opacity=Math.Max(0,Math.Min(1,(heartUntil-t)/.65));if(heart.Opacity>0)Canvas.SetTop(heart,Math.Max(0,Height-PoseHeight()-35*scale)-(1.6-(heartUntil-t))*13);}
  void OnDown(object sender,MouseButtonEventArgs e){if(e.ClickCount==2){autoPose=false;SetPose(pose=="sit"?"relax":pose=="relax"?"sleep":"sit");SaveSettings();e.Handled=true;return;}Drawing.Point p=Forms.Cursor.Position;dragX=p.X;dragY=p.Y;leftDown=Left;topDown=Top;dragging=true;moved=false;cat.CaptureMouse();e.Handled=true;}
  void OnMove(object sender,MouseEventArgs e){if(!dragging||e.LeftButton!=MouseButtonState.Pressed)return;Drawing.Point p=Forms.Cursor.Position;double dx=(p.X-dragX)/dpi,dy=(p.Y-dragY)/dpi;if(Math.Abs(dx)+Math.Abs(dy)>4)moved=true;if(moved){Left=leftDown+dx;Top=topDown+dy;}e.Handled=true;}
  void OnUp(object sender,MouseButtonEventArgs e){if(!dragging)return;dragging=false;cat.ReleaseMouseCapture();if(!moved){heartUntil=clock.Elapsed.TotalSeconds+1.6;}ClampPosition();SaveSettings();e.Handled=true;}
  Rect WorkArea(){Drawing.Rectangle r=Forms.Screen.FromRectangle(new Drawing.Rectangle((int)(Left*dpi),(int)(Top*dpi),Math.Max(1,(int)(Width*dpi)),Math.Max(1,(int)(Height*dpi)))).WorkingArea;return new Rect(r.X/dpi,r.Y/dpi,r.Width/dpi,r.Height/dpi);}
  public static Point Constrain(double x,double y,double width,double height,Rect work){return new Point(Math.Max(work.Left,Math.Min(x,work.Right-width)),Math.Max(work.Top,Math.Min(y,work.Bottom-height)));}
  void ClampPosition(){if(!ready||double.IsNaN(Left)||double.IsNaN(Top))return;Point p=Constrain(Left,Top,Width,Height,WorkArea());Left=p.X;Top=p.Y;}
  void ResetPosition(){Drawing.Rectangle r=Forms.Screen.PrimaryScreen.WorkingArea;Left=r.Right/dpi-Width-18;Top=r.Bottom/dpi-Height;ClampPosition();SaveSettings();}
  void ReadSettings(){if(testing||!File.Exists(settingsPath))return;try{foreach(string line in File.ReadAllLines(settingsPath)){string[] p=line.Split('=');if(p.Length!=2)continue;double v;bool b;if(p[0]=="scale"&&double.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out v)&&!double.IsNaN(v)&&!double.IsInfinity(v))scale=Math.Max(.5,Math.Min(1.6,v));if(p[0]=="left"&&double.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out v)&&!double.IsNaN(v)&&!double.IsInfinity(v))Left=v;if(p[0]=="top"&&double.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out v)&&!double.IsNaN(v)&&!double.IsInfinity(v))Top=v;if(p[0]=="pose"&&poses.ContainsKey(p[1]))pose=p[1];if(p[0]=="auto"&&bool.TryParse(p[1],out b))autoPose=b;if(p[0]=="motion"&&bool.TryParse(p[1],out b))motion=b;if(p[0]=="topmost"&&bool.TryParse(p[1],out b))Topmost=b;}}catch(IOException){}catch(UnauthorizedAccessException){}}
  void SaveSettings(){if(testing||!ready)return;try{string[] lines={"pose="+pose,"scale="+scale.ToString(CultureInfo.InvariantCulture),"left="+Left.ToString(CultureInfo.InvariantCulture),"top="+Top.ToString(CultureInfo.InvariantCulture),"auto="+autoPose,"motion="+motion,"topmost="+Topmost};File.WriteAllLines(settingsPath,lines);}catch(IOException){}catch(UnauthorizedAccessException){}}
  void ShowHelp(){MessageBox.Show("點點桌寵 v"+Version+"\n\n拖曳點點：移動位置\n點一下：摸摸\n連點兩下：換姿勢\n滑鼠滾輪：調整大小\n右鍵：姿勢、呼吸、自動切換、隱藏與結束\n隱藏後可從工作列右下角圖示叫回\n\n本版為真實照片經 AI 輔助去背／重建的三種姿勢，細節可能與原照略有差異。呼吸是程式效果，尚未加入原始影片的走路、舔毛或轉身動畫。\n\n程式離線執行；不讀取照片庫、不連網、不自動開機啟動。","點點桌寵",MessageBoxButton.OK,MessageBoxImage.Information);}
  public void RunChecks(string dir){Directory.CreateDirectory(dir);List<string> log=new List<string>();stage.Background=new SolidColorBrush(Color.FromRgb(238,237,231));if(!AllowsTransparency||WindowStyle!=WindowStyle.None||ShowInTaskbar)throw new Exception("Window setup");log.Add("PASS transparent borderless window and hidden taskbar button");
   foreach(string key in new string[]{"sit","relax","sleep"}){BitmapSource b=poses[key];FormatConvertedBitmap a=new FormatConvertedBitmap(b,PixelFormats.Bgra32,null,0);byte[] p=new byte[b.PixelWidth*b.PixelHeight*4];a.CopyPixels(p,b.PixelWidth*4,0);int empty=0,opaque=0;for(int i=3;i<p.Length;i+=4){if(p[i]==0)empty++;if(p[i]>240)opaque++;}if(empty<1000||opaque<10000)throw new Exception("Invalid alpha "+key);SetPose(key);stage.Measure(new Size(Width,Height));stage.Arrange(new Rect(0,0,Width,Height));UpdateLayout();DispatcherFrame flush=new DispatcherFrame();Dispatcher.BeginInvoke(new Action(delegate{flush.Continue=false;}),DispatcherPriority.ContextIdle);Dispatcher.PushFrame(flush);log.Add("LAYOUT "+key+" window="+Width+"x"+Height+" cat="+cat.Width+"x"+cat.Height+" actual="+cat.ActualWidth+"x"+cat.ActualHeight+" pos="+Canvas.GetLeft(cat)+","+Canvas.GetTop(cat)+" dpi="+dpi);RenderTargetBitmap rt=new RenderTargetBitmap((int)Width,(int)Height,96,96,PixelFormats.Pbgra32);rt.Render(stage);PngBitmapEncoder en=new PngBitmapEncoder();en.Frames.Add(BitmapFrame.Create(rt));using(Stream st=File.Create(Path.Combine(dir,"preview-"+key+".png")))en.Save(st);log.Add("PASS pose "+key+", transparent pixels="+empty+", opaque pixels="+opaque);}
   stage.Background=null;Point cl=Constrain(-9999,9999,420,360,new Rect(-1920,0,1920,1040));if(cl.X!=-1920||cl.Y!=680)throw new Exception("Negative monitor bounds");log.Add("PASS screen clamp including negative-coordinate monitor");ResizePet(9);if(scale!=1.6)throw new Exception("upper scale");ResizePet(.01);if(scale!=.5)throw new Exception("lower scale");ResizePet(1);log.Add("PASS size limits and layout");autoPose=false;motion=false;Tick(null,EventArgs.Empty);if(breathing.ScaleY!=1)throw new Exception("pause motion");log.Add("PASS breath pause");autoPose=true;nextPose=-1;string prior=pose;Tick(null,EventArgs.Empty);if(prior==pose)throw new Exception("auto pose");log.Add("PASS automatic pose transition");if(menu.Items.Count<12)throw new Exception("menu");log.Add("PASS menu controls created");BuildTray();if(tray==null||!tray.Visible)throw new Exception("tray");tray.Visible=false;tray.Dispose();tray=null;log.Add("PASS tray creation and cleanup");log.Add("No source photos were opened or modified by the application.");File.WriteAllLines(Path.Combine(dir,"self-test.txt"),log);}
 }
}
