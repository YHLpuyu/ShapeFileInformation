void LoadDBF()
{
    string path = @"city.dbf";
    ShapeDBF sd = new ShapeDBF(path);
    DataTable dbfTable = sd.GetInfoFromDBF();
}

void DrawFeatures()
{
   string path = @"New_Shapefile.dbf";
   ShapeFile sfh = new ShapeFile(path);
   sfh.ReadHeader();
   sfh.ReadRecord();
   
   //将图层中的所有几何对象绘制到当前绘制区域内
   Graphics g = this.CreateGraphics();//这里用Form来绘制几何对象
   int width = this.ClientRectangle.Width;
   int height = this.ClientRectangle.Height;
   double layerWidth = sfh.Envelope.East - sfh.Envelope.West;
   double layerHeight = sfh.Envelope.North - sfh.Envelope.South;
   System.Diagnostics.Debug.WriteLine(sfh.Envelope.West + "," + sfh.Envelope.East + ","
       + sfh.Envelope.South + "," + sfh.Envelope.North);
   Brush polygonBrush = new SolidBrush(Color.Chocolate);//多边形填充色
   Pen pen = new Pen(Color.Red);//点和线的颜色
   foreach(ShapeRecord sr in sfh.Shapes)
   {
       if(sr.Polygon!=null)
       {
           List<PointF> points = sr.Polygon.Points.ToList().Select(item =>
           {
               PointF point = new PointF();
               point.X = (float)((item.X - sfh.Envelope.West) * width / layerWidth);
               point.Y = (float)((item.Y - sfh.Envelope.South) * height / layerHeight);
               point.Y = (float)height - point.Y;
               return point;
           }).ToList();
           g.FillPolygon(polygonBrush, points.ToArray());
       }
       else if(sr.Point!=null)
       {
           PointF point = new PointF();
           point.X = (float)((sr.Point.X - sfh.Envelope.West) * width / layerWidth);
           point.Y = (float)((sr.Point.Y - sfh.Envelope.South) * height / layerHeight);
           point.Y = (float)height - point.Y;
           g.FillEllipse(polygonBrush, point.X, point.Y, 2, 2);
       }
       else if(sr.MultiPoint!=null)
       {
           sr.MultiPoint.Points.ToList().ForEach(item =>
           {
               PointF point = new PointF();
               point.X = (float)((item.X - sfh.Envelope.West) * width / layerWidth);
               point.Y = (float)((item.Y - sfh.Envelope.South) * height / layerHeight);
               point.Y = (float)height - point.Y;
               g.FillEllipse(polygonBrush, point.X, point.Y, 2, 2);
           });
       }
       else if(sr.PolyLine!=null)
       {
           if(sr.PolyLine.NumParts==1)
           {
               List<PointF> points = sr.PolyLine.Points.ToList().Select(item =>
               {
                   PointF point = new PointF();
                   point.X = (float)((item.X - sfh.Envelope.West) * width / layerWidth);
                   point.Y = (float)((item.Y - sfh.Envelope.South) * height / layerHeight);
                   point.Y = (float)height - point.Y;
                   return point;
               }).ToList();
               g.DrawLines(pen, points.ToArray());
           }
           else if(sr.PolyLine.NumParts>1)
           {
               for(int i=0;i<sr.PolyLine.Parts.Length;i++)
               {
                   List<PointF> points = null;
                   if (i<sr.PolyLine.Parts.Length-1)
                   {
                       points= sr.PolyLine.Points.ToList().GetRange(sr.PolyLine.Parts[i], sr.PolyLine.Parts[i + 1] - sr.PolyLine.Parts[i]).Select(item =>
                       {
                           PointF point = new PointF();
                           point.X = (float)((item.X - sfh.Envelope.West) * width / layerWidth);
                           point.Y = (float)((item.Y - sfh.Envelope.South) * height / layerHeight);
                           point.Y = (float)height - point.Y;
                           return point;
                       }).ToList();
                   }
                   else
                   {
                       points = sr.PolyLine.Points.ToList().GetRange(sr.PolyLine.Parts[i], sr.PolyLine.Points.Count() - sr.PolyLine.Parts[i]).Select(item =>
                       {
                           PointF point = new PointF();
                           point.X = (float)((item.X - sfh.Envelope.West) * width / layerWidth);
                           point.Y = (float)((item.Y - sfh.Envelope.South) * height / layerHeight);
                           point.Y = (float)height - point.Y;
                           return point;
                       }).ToList();
                   }
                   g.DrawLines(pen, points.ToArray());
               }
           }
       }
   }
}

