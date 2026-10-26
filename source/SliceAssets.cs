using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Imaging;using System.Runtime.InteropServices;using System.IO;
class SliceAssets {
 static void Main(string[] args) {
  using(Bitmap src=new Bitmap(args[0]))using(Bitmap im=new Bitmap(src.Width,src.Height,PixelFormat.Format32bppArgb)) {
   using(Graphics g=Graphics.FromImage(im))g.DrawImageUnscaled(src,0,0);
   int w=im.Width,h=im.Height,n=w*h;BitmapData bd=im.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);byte[] p=new byte[n*4];Marshal.Copy(bd.Scan0,p,0,p.Length);im.UnlockBits(bd);
   int[] labels=new int[n],queue=new int[n];List<int[]> comps=new List<int[]>();int id=0;
   for(int k=0;k<n;k++){if(p[k*4+3]<16||labels[k]!=0)continue;id++;int head=0,tail=0,minx=w,miny=h,maxx=0,maxy=0;queue[tail++]=k;labels[k]=id;
    while(head<tail){int v=queue[head++],x=v%w,y=v/w;minx=Math.Min(minx,x);miny=Math.Min(miny,y);maxx=Math.Max(maxx,x);maxy=Math.Max(maxy,y);for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int xx=x+dx,yy=y+dy;if(xx<0||yy<0||xx>=w||yy>=h)continue;int u=yy*w+xx;if(labels[u]==0&&p[u*4+3]>=16){labels[u]=id;queue[tail++]=u;}}}
    comps.Add(new int[]{id,tail,minx,miny,maxx,maxy});
   }
   comps.Sort((a,b)=>b[1].CompareTo(a[1]));if(comps.Count<2||comps[1][1]<10000)throw new Exception("Two distinct poses required");int[] first=comps[0],second=comps[1];if(first[2]>second[2]){int[] t=first;first=second;second=t;}int[][] chosen={first,second};string[] names={"relax.png","sleep.png"};Directory.CreateDirectory(args[1]);
   for(int j=0;j<2;j++){int[] c=chosen[j];int x0=Math.Max(0,c[2]-16),y0=Math.Max(0,c[3]-16),x1=Math.Min(w,c[4]+17),y1=Math.Min(h,c[5]+17);int ow=x1-x0,oh=y1-y0;byte[] dst=new byte[ow*oh*4];
    for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){int k=y*w+x;if(p[k*4+3]==0)continue;bool keep=labels[k]==c[0];if(!keep&&labels[k]!=chosen[1-j][0])for(int dy=-2;dy<=2&&!keep;dy++)for(int dx=-2;dx<=2&&!keep;dx++){int xx=x+dx,yy=y+dy;if(xx>=0&&xx<w&&yy>=0&&yy<h&&labels[yy*w+xx]==c[0])keep=true;}if(keep)Buffer.BlockCopy(p,k*4,dst,((y-y0)*ow+x-x0)*4,4);}
    using(Bitmap output=new Bitmap(ow,oh,PixelFormat.Format32bppArgb)){BitmapData ob=output.LockBits(new Rectangle(0,0,ow,oh),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);Marshal.Copy(dst,0,ob.Scan0,dst.Length);output.UnlockBits(ob);output.Save(Path.Combine(args[1],names[j]),ImageFormat.Png);}Console.WriteLine(names[j]+" "+ow+"x"+oh+" component pixels="+c[1]);
   }
  }
 }
}
