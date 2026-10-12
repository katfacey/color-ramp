using System.Drawing.Drawing2D;
namespace ColorRamp;
internal sealed class ColorPickerDialog:Form
{
    readonly ColorWheel wheel=new();
    readonly ValueSlider alpha=new(){Minimum=0,Maximum=1,DecimalPlaces=3,Increment=.01m};
    readonly RoundedTextBox hex=new();
    readonly RoundedButton preview=new();
    bool updating;
    public Color SelectedColor=>wheel.SelectedColor;
    public double SelectedAlpha=>(double)alpha.Value;
    public ColorPickerDialog(Color color,double opacity,bool dark)
    {
        Text="Color";Font=new Font("Segoe UI",10);ClientSize=new Size(300,402);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
        BackColor=dark?Color.FromArgb(39,41,44):Color.FromArgb(242,243,245);ForeColor=dark?Color.FromArgb(236,237,239):Color.FromArgb(30,32,35);var field=dark?Color.FromArgb(57,60,64):Color.White;
        var title=new Label{Text="Color",Bounds=new Rectangle(18,12,260,25),ForeColor=ForeColor};Controls.Add(title);
        wheel.Bounds=new Rectangle(18,42,264,230);wheel.SetColor(color);Controls.Add(wheel);
        preview.Bounds=new Rectangle(18,280,264,28);preview.TabStop=false;preview.BackColor=color;preview.FlatAppearance.BorderColor=field;Controls.Add(preview);
        Controls.Add(new Label{Text="HEX",Bounds=new Rectangle(18,319,36,25),ForeColor=ForeColor});hex.Bounds=new Rectangle(58,314,108,30);hex.BackColor=field;hex.ForeColor=ForeColor;Controls.Add(hex);
        Controls.Add(new Label{Text="A",Bounds=new Rectangle(176,319,20,25),ForeColor=ForeColor});alpha.Bounds=new Rectangle(201,314,81,30);alpha.BackColor=field;alpha.ForeColor=ForeColor;alpha.Value=(decimal)opacity;Controls.Add(alpha);
        var ok=new RoundedButton{Text="OK",Bounds=new Rectangle(146,356,64,32),DialogResult=DialogResult.OK,BackColor=field,ForeColor=ForeColor};var cancel=new RoundedButton{Text="Cancel",Bounds=new Rectangle(218,356,64,32),DialogResult=DialogResult.Cancel,BackColor=field,ForeColor=ForeColor};Controls.Add(ok);Controls.Add(cancel);AcceptButton=ok;CancelButton=cancel;
        wheel.ColorChanged+=(_,_)=>RefreshColor();hex.Leave+=(_,_)=>ApplyHex();hex.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){ApplyHex();e.SuppressKeyPress=true;}};RefreshColor();
    }
    void RefreshColor(){updating=true;hex.Text=RampDocument.ToHex(SelectedColor);preview.BackColor=SelectedColor;preview.Invalidate();updating=false;}
    void ApplyHex(){if(updating)return;if(RampDocument.TryHex(hex.Text,out var color))wheel.SetColor(color);RefreshColor();}
}
internal sealed class ColorWheel:Control
{
    float hue,saturation,value=1;
    Bitmap? image;
    int dragging;
    public event EventHandler? ColorChanged;
    public Color SelectedColor=>FromHsv(hue,saturation,value);
    public ColorWheel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);TabStop=true;AccessibleName="Hue and saturation wheel with brightness slider";}
    public void SetColor(Color color)
    {
        hue=color.GetHue()/360;saturation=color.GetSaturation();
        float min=Math.Min(color.R,Math.Min(color.G,color.B))/255f,max=Math.Max(color.R,Math.Max(color.G,color.B))/255f;
        saturation=max==0?0:(max-min)/max;value=max;image?.Dispose();image=null;Invalidate();ColorChanged?.Invoke(this,EventArgs.Empty);
    }
    Rectangle WheelBounds=>new(0,0,Math.Min(Width-34,Height),Math.Min(Width-34,Height));
    protected override void OnPaint(PaintEventArgs e)
    {
        var r=WheelBounds;int size=r.Width;float radius=(size-2)/2f;
        if(image==null||image.Width!=size){image?.Dispose();image=new Bitmap(size,size);for(int y=0;y<size;y++)for(int x=0;x<size;x++){float dx=x-size/2f,dy=y-size/2f,distance=MathF.Sqrt(dx*dx+dy*dy);if(distance<=radius){float h=(MathF.Atan2(-dy,dx)/(2*MathF.PI)+1)%1;image.SetPixel(x,y,FromHsv(h,distance/radius,value));}}}
        var g=e.Graphics;g.Clear(Parent?.BackColor??BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;g.DrawImageUnscaled(image,0,0);
        float angle=hue*2*MathF.PI,cx=size/2f+MathF.Cos(angle)*saturation*radius,cy=size/2f-MathF.Sin(angle)*saturation*radius;
        using(var pen=new Pen(Color.Black,3))g.DrawEllipse(pen,cx-5,cy-5,10,10);using(var pen=new Pen(Color.White,1))g.DrawEllipse(pen,cx-5,cy-5,10,10);
        int barX=size+10;for(int y=0;y<size;y++){using var pen=new Pen(FromHsv(hue,saturation,1-y/(float)Math.Max(1,size-1)));g.DrawLine(pen,barX,y,barX+20,y);}
        using(var pen=new Pen(Color.Gray))g.DrawRectangle(pen,barX,0,20,size-1);float marker=(1-value)*(size-1);using(var pen=new Pen(Color.Black,4))g.DrawLine(pen,barX-2,marker,barX+22,marker);using(var pen=new Pen(Color.White,2))g.DrawLine(pen,barX-2,marker,barX+22,marker);
    }
    void Adjust(Point point)
    {
        int size=WheelBounds.Width;float radius=(size-2)/2f;
        if(dragging==2)value=1-Math.Clamp(point.Y/(float)Math.Max(1,size-1),0,1);
        else{float dx=point.X-size/2f,dy=point.Y-size/2f;hue=(MathF.Atan2(-dy,dx)/(2*MathF.PI)+1)%1;saturation=Math.Clamp(MathF.Sqrt(dx*dx+dy*dy)/radius,0,1);}
        image?.Dispose();image=null;Invalidate();ColorChanged?.Invoke(this,EventArgs.Empty);
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();dragging=e.X>=WheelBounds.Width?2:1;Capture=true;Adjust(e.Location);}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture&&dragging!=0)Adjust(e.Location);}
    protected override void OnMouseUp(MouseEventArgs e){Capture=false;dragging=0;base.OnMouseUp(e);}
    protected override void Dispose(bool disposing){if(disposing)image?.Dispose();base.Dispose(disposing);}
    internal static Color FromHsv(float h,float s,float v)
    {
        h=(h%1+1)%1;s=Math.Clamp(s,0,1);v=Math.Clamp(v,0,1);float c=v*s,x=c*(1-Math.Abs(h*6%2-1)),m=v-c;
        var (r,g,b)=(int)(h*6) switch{0=>(c,x,0f),1=>(x,c,0f),2=>(0f,c,x),3=>(0f,x,c),4=>(x,0f,c),_=>(c,0f,x)};
        return Color.FromArgb((int)Math.Round((r+m)*255),(int)Math.Round((g+m)*255),(int)Math.Round((b+m)*255));
    }
}
