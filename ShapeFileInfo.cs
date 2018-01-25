using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Threading.Tasks;

namespace ShapeFileInfo
{
    public class ShapeFile
    {
        string dbfPath;

        public byte Version { get; set; }
        byte UpdateYear { get; set; }
        byte UpdateMonth { get; set; }
        byte UpdateDay { get; set; }
        public int RecordCount { get; set; }
        public int FieldCount { get; set; }
        public Extent Envelope { get; private set; }

        ArrayList metaValues = new ArrayList();
        /// <summary>
        /// shp几何对象
        /// </summary>
        public List<ShapeRecord> Shapes { get; private set; } = new List<ShapeRecord>();

        public ShapeFile(string path)
        {
            dbfPath = path;
        }

        public void ReadHeader()
        {
            FileInfo dbfFile = new FileInfo(dbfPath);
            using (BinaryReader dbfReader = new BinaryReader(new BufferedStream(dbfFile.OpenRead()), System.Text.Encoding.ASCII))
            {
                int bytesRead = 0;
                Version = dbfReader.ReadByte();
                UpdateYear = dbfReader.ReadByte();
                UpdateMonth = dbfReader.ReadByte();
                UpdateDay = dbfReader.ReadByte();
                RecordCount = dbfReader.ReadInt32();
                short headerLength = dbfReader.ReadInt16();
                short recordLength = dbfReader.ReadInt16();
                byte[] reserved = dbfReader.ReadBytes(20);
                bytesRead += 32;
                FieldCount = (headerLength - 33) / 32;

                DBFField[] fieldHeaders = new DBFField[FieldCount];
                for(int i=0;i<FieldCount;i++)
                {
                    char[] fieldNameChars = dbfReader.ReadChars(10);
                    char fieldNameTerminator = dbfReader.ReadChar();
                    string fn = new string(fieldNameChars);
                    fieldHeaders[i].FieldName = fn.Trim().Replace(" ", "");
                    fieldHeaders[i].FieldType = dbfReader.ReadChar();
                    byte[] reserved1 = dbfReader.ReadBytes(4);
                    fieldHeaders[i].FieldLength = dbfReader.ReadByte();
                    byte[] reserved2 = dbfReader.ReadBytes(15);
                    bytesRead += 32;
                }
                byte headerTerminator = dbfReader.ReadByte();
                for(int i=0;i<RecordCount;i++)
                {
                    byte isValid = dbfReader.ReadByte();
                    for(int j=0;j<fieldHeaders.Length;j++)
                    {
                        char[] fieldDataChars = dbfReader.ReadChars(fieldHeaders[j].FieldLength);
                        string fieldData = new string(fieldDataChars);
                        if(j==0) metaValues.Add(fieldData);
                    }
                }
            }
        }
        public void ReadRecord()
        {
            FileInfo shapeFile = new FileInfo(dbfPath.Replace(".dbf", ".shp"));
            using (FileStream fs = File.OpenRead(shapeFile.FullName))
            {
                using (BinaryReader reader = new BinaryReader(new BufferedStream(fs)))
                {
                    //Big-Endian Interger File Type,大字节序
                    byte[] fileTypeBytes = reader.ReadBytes(4);
                    int fileType = 16 * 16 * 16 * 16 * 16 * 16 * fileTypeBytes[0] + 16 * 16 * 16 * 16 * fileTypeBytes[1]
                        + 16 * 16 * fileTypeBytes[2] + fileTypeBytes[3];

                    //20个保留字节
                    byte[] unused1 = reader.ReadBytes(5 * 4);

                    byte[] fileLengthBytes = reader.ReadBytes(4);
                    int fileLength = 16 * 16 * 16 * 16 * 16 * 16 * fileLengthBytes[0] + 16 * 16 * 16 * 16 * fileLengthBytes[1] +
                        16 * 16 * fileLengthBytes[2] + fileLengthBytes[3];
                    int version = reader.ReadInt32();
                    int shapeType = reader.ReadInt32();
                    Extent boundingBox = new Extent();
                    boundingBox.West = reader.ReadDouble();
                    boundingBox.South = reader.ReadDouble();
                    boundingBox.East = reader.ReadDouble();
                    boundingBox.North = reader.ReadDouble();
                    boundingBox.ZMin = reader.ReadDouble();
                    boundingBox.ZMax = reader.ReadDouble();
                    boundingBox.MMin = reader.ReadDouble();
                    boundingBox.MMax = reader.ReadDouble();
                    Envelope = boundingBox;
                    //开始读取记录
                    int bytesRead = 100;
                    int counter = 0;
                    while (bytesRead < shapeFile.Length)
                    {
                        ArrayList pendingPoints = new ArrayList();

                        //读取记录信息
                        byte[] recordNumberBytes = reader.ReadBytes(4);
                        byte[] contentLengthBytes = reader.ReadBytes(4);
                        int recordNumber = 16 * 16 * 16 * 16 * 16 * 16 * recordNumberBytes[0] + 16 * 16 * 16 * 16 * recordNumberBytes[1] +
                            16 * 16 * recordNumberBytes[2] + recordNumberBytes[3];
                        int contentLength = 16 * 16 * 16 * 16 * 16 * 16 * contentLengthBytes[0] + 16 * 16 * 16 * 16 * contentLengthBytes[1] +
                            16 * 16 * contentLengthBytes[2] + contentLengthBytes[3];
                        int recordShapeType = reader.ReadInt32();
                        ShapeRecord newRecord = new ShapeRecord();
                        switch(recordShapeType)
                        {
                            case 0:
                                //Null shape type, placeholder 占位符
                                newRecord.Null = new Shapefile_Null();
                                break;
                            case 1:
                                //Point shape type
                                newRecord.Point = new Shapefile_Point();
                                newRecord.Point.X = reader.ReadDouble();
                                newRecord.Point.Y = reader.ReadDouble();
                                break;
                            case 8:
                                newRecord.MultiPoint = new Shapefile_MultiPoint();
                                newRecord.MultiPoint.BoundingBox.West = reader.ReadDouble();
                                newRecord.MultiPoint.BoundingBox.South = reader.ReadDouble();
                                newRecord.MultiPoint.BoundingBox.East = reader.ReadDouble();
                                newRecord.MultiPoint.BoundingBox.North = reader.ReadDouble();
                                newRecord.MultiPoint.NumPoints = reader.ReadInt32();
                                newRecord.MultiPoint.Points = new Shapefile_Point[newRecord.MultiPoint.NumPoints];
                                for(int i=0;i<newRecord.MultiPoint.NumPoints;i++)
                                {
                                    newRecord.MultiPoint.Points[i] = new Shapefile_Point();
                                    newRecord.MultiPoint.Points[i].X = reader.ReadDouble();
                                    newRecord.MultiPoint.Points[i].Y = reader.ReadDouble();
                                }
                                break;
                            case 3:
                                newRecord.PolyLine = new Shapefile_PolyLine();
                                newRecord.PolyLine.BoundingBox.West = reader.ReadDouble();
                                newRecord.PolyLine.BoundingBox.South = reader.ReadDouble();
                                newRecord.PolyLine.BoundingBox.East = reader.ReadDouble();
                                newRecord.PolyLine.BoundingBox.North = reader.ReadDouble();
                                newRecord.PolyLine.NumParts = reader.ReadInt32();
                                newRecord.PolyLine.NumPoints = reader.ReadInt32();
                                newRecord.PolyLine.Parts = new int[newRecord.PolyLine.NumParts];
                                for(int i=0;i<newRecord.PolyLine.Parts.Length;i++)
                                {
                                    newRecord.PolyLine.Parts[i] = reader.ReadInt32();
                                }
                                newRecord.PolyLine.Points = new Shapefile_Point[newRecord.PolyLine.NumPoints];
                                for(int i=0;i<newRecord.PolyLine.Points.Length;i++)
                                {
                                    newRecord.PolyLine.Points[i] = new Shapefile_Point();
                                    newRecord.PolyLine.Points[i].X = reader.ReadDouble();
                                    newRecord.PolyLine.Points[i].Y = reader.ReadDouble();
                                }
                                break;
                            case 5:
                                newRecord.Polygon = new Shapefile_Polygon();
                                newRecord.Polygon.BoundingBox.West = reader.ReadDouble();
                                newRecord.Polygon.BoundingBox.South = reader.ReadDouble();
                                newRecord.Polygon.BoundingBox.East = reader.ReadDouble();
                                newRecord.Polygon.BoundingBox.North = reader.ReadDouble();
                                newRecord.Polygon.NumParts = reader.ReadInt32();
                                newRecord.Polygon.NumPoints = reader.ReadInt32();
                                newRecord.Polygon.Parts = new int[newRecord.Polygon.NumParts];
                                for(int i=0;i<newRecord.Polygon.Parts.Length;i++)
                                {
                                    newRecord.Polygon.Parts[i] = reader.ReadInt32();
                                }
                                newRecord.Polygon.Points = new Shapefile_Point[newRecord.Polygon.NumPoints];
                                for(int i=0;i<newRecord.Polygon.Points.Length;i++)
                                {
                                    newRecord.Polygon.Points[i] = new Shapefile_Point();
                                    newRecord.Polygon.Points[i].X = reader.ReadDouble();
                                    newRecord.Polygon.Points[i].Y = reader.ReadDouble();
                                }
                                break;
                        }
                        newRecord.Value = metaValues[counter];
                        bytesRead += 8 + contentLength * 2;
                        counter++;
                        Shapes.Add(newRecord);
                    }
                }
            }
        }
    }
    public class ShapeRecord
    {
        #region Private Members
        string m_Id;
        Shapefile_Null m_Null = null;
        Shapefile_Point m_Point = null;
        Shapefile_MultiPoint m_MultiPoint = null;
        Shapefile_PolyLine m_PolyLine = null;
        Shapefile_Polygon m_Polygon = null;
        object m_Value = null;
        #endregion

        #region Properties
        public string ID
        {
            get
            {
                return m_Id;
            }
            set
            {
                m_Id = value;
            }
        }
        public Shapefile_Null Null
        {
            get
            {
                return m_Null;
            }
            set
            {
                m_Null = value;
            }
        }
        public Shapefile_Point Point
        {
            get
            {
                return m_Point;
            }
            set
            {
                m_Point = value;
            }
        }
        public Shapefile_MultiPoint MultiPoint
        {
            get
            {
                return m_MultiPoint;
            }
            set
            {
                m_MultiPoint = value;
            }
        }
        public Shapefile_PolyLine PolyLine
        {
            get
            {
                return m_PolyLine;
            }
            set
            {
                m_PolyLine = value;
            }
        }
        public Shapefile_Polygon Polygon
        {
            get
            {
                return m_Polygon;
            }
            set
            {
                m_Polygon = value;
            }
        }
        public object Value
        {
            get
            {
                return m_Value;
            }
            set
            {
                m_Value = value;
            }
        }
        #endregion
    }
    public class Shapefile_Null { }
    public class Shapefile_Point
    {
        public double X;
        public double Y;
        public object Tag = null;
    }
    public class Shapefile_MultiPoint
    {
        public Extent BoundingBox = new Extent();
        public int NumPoints;
        public Shapefile_Point[] Points;
    }
    public class Shapefile_PolyLine
    {
        public Extent BoundingBox = new Extent();
        public int NumParts;//部件数
        public int NumPoints;
        public int[] Parts;//每个部件的起始点下标
        public Shapefile_Point[] Points;
    }
    public class Shapefile_Polygon
    {
        public Extent BoundingBox = new Extent();
        public int NumParts;//部件数
        public int NumPoints;
        public int[] Parts;//每个部件的起始点下标
        public Shapefile_Point[] Points;
    }
    struct DBFField
    {
        public string FieldName;
        public char FieldType;
        public byte FieldLength;
    }
    public struct Extent
    {
        public double North;
        public double South;
        public double East;
        public double West;
        public double ZMax;
        public double ZMin;
        public double MMax;
        public double MMin;
    }
    public class ShapeDBF
    {
        string dbfpath;
        public ShapeDBF(string path)
        {
            dbfpath = path;
        }
        public DataTable GetInfoFromDBF()
        {
            try
            {
                string connectionString = "Driver={Microsoft dBASE Driver (*.dbf)};DBQ=" +
                Path.GetDirectoryName(Path.GetFullPath(dbfpath));
                OdbcConnection conn = new OdbcConnection(connectionString);
                OdbcCommand command = new OdbcCommand("SELECT * FROM " + Path.GetFileNameWithoutExtension(dbfpath), conn);
                DataSet ds = new DataSet();
                OdbcDataAdapter da = new OdbcDataAdapter(command);
                da.Fill(ds);
                return ds.Tables[0];    
            }
            catch
            {
                return null;
            }
        }
    }
}
