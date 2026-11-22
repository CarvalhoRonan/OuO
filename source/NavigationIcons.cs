using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
namespace Aurora {
public static class NavigationIcons {
    static readonly Dictionary<string,string> paths=new Dictionary<string,string>(StringComparer.Ordinal){
        {"dashboard","M2,3H10V13H2Z M14,3H22V8H14Z M14,12H22V21H14Z M2,17H10V21H2Z"},
        {"recommendations","M8,17C8,13 4,13 4,8C4,-1 20,-1 20,8C20,13 16,13 16,17Z M8,20H16 M10,23H14 M12,3V6 M8,7L10,9 M16,7L14,9"},
        {"performance","M2,6H22V17H2Z M6,6V11H10V6 M14,6V11H18V6 M5,17V21 M9,17V21 M15,17V21 M19,17V21"},
        {"processes","M2,3H22V21H2Z M2,7H22 M6,17V13 M12,17V10 M18,17V12"},
        {"stability","M2,19V3 M2,19H22 M5,13L9,9L12,15L16,5L21,9 M16,5H21"},
        {"tweaks","M2,5H13 M18,5H22 M14,2H18V8H14Z M2,12H6 M10,12H22 M6,9H10V15H6Z M2,19H14 M18,19H22 M14,16H18V22H14Z"},
        {"repair","M15,3A6,6 0 0 0 8,11L2,17A3,3 0 0 0 7,22L13,16A6,6 0 0 0 21,9L17,12L12,7Z M5,19L5.2,19"},
        {"protection","M12,2L22,6L20,16Q17,21 12,23Q7,21 4,16L2,6Z M8,11V9A4,4 0 0 1 16,9V11 M7,11H17V17H7Z M12,13V15"},
        {"browser","M20,12A9,9 0 1 0 12,21 M12,3C6,8 6,17 12,21 M12,3C16,6 17,9 17,12 M3,12H19 M5,7H19 M5,17H12 M15,18L18,21L23,15"},
        {"network","M6,9V2H18V9 M6,17H3V9H21V17H18 M6,14H18V22H6Z M17,11H18 M9,17H15 M9,20H15"},
        {"storage","M3,2H21V22H3Z M18,11A6,6 0 1 0 6,11A6,6 0 1 0 18,11 M12,11L18,6 M6,19H14 M18,19L18.1,19"},
        {"cloud-storage","M6,18H18A4,4 0 0 0 18,10A6,6 0 0 0 6,9A4.5,4.5 0 0 0 6,18Z M9,13H15 M9,15.5H12"},
        {"cleanup","M15,2L10,12 M8,11L16,15L12,23L2,18Z M6,13L3,19 M10,15L7,21 M18,3V7 M16,5H20 M20,11V15 M18,13H22"},
        {"startup","M9,4C13,1 18,1 21,2C22,7 20,12 16,15L9,14Z M9,6L4,9L3,15L9,13 M16,15L15,21L9,22L10,15 M16,6A2,2 0 1 0 16,10A2,2 0 1 0 16,6 M7,17L3,22 M5,16L2,19"},
        {"apps","M3,3H9V9H3Z M15,3H21V9H15Z M3,15H9V21H3Z M18,13V22 M14,18L18,22L22,18"},
        {"office","M5,2H15L21,8V22H5Z M15,2V8H21 M9,11V18H13V11Z M16,12V18 M2,6V19"},
        {"bloat","M3,6L12,2L21,6V18L12,22L3,18Z M3,6L12,10L21,6 M12,10V22 M6,4L16,8 M6,14H10"},
        {"recovery","M2,4H7V22H2Z M9,2H14V22H9Z M17,4L22,3L23,21L18,22Z M3,8H6 M10,6H13 M18,8L21,7"},
        {"drivers","M6,6H18V18H6Z M9,2V6 M15,2V6 M9,18V22 M15,18V22 M2,9H6 M2,15H6 M18,9H22 M18,15H22 M12,8V16 M9,13L12,16L15,13"},
        {"upgrade","M2,4H10V11H2Z M13,3H21V11H13Z M2,14H10V21H2Z M14,18L18,14L22,18 M18,14V23"},
        {"settings","M9,2H15L16,6L20,7L23,12L20,17L16,18L15,22H9L8,18L4,17L1,12L4,7L8,6Z M16,12A4,4 0 1 0 8,12A4,4 0 1 0 16,12"},
        {"history","M3,7A9,9 0 1 1 3,17 M3,2V8H9 M12,6V12L17,15"},
        {"about","M22,12A10,10 0 1 0 2,12A10,10 0 1 0 22,12 M12,6V7 M10,10H12V18 M9,18H15"},
        {"sun","M16,12A4,4 0 1 0 8,12A4,4 0 1 0 16,12 M12,1V4 M12,20V23 M1,12H4 M20,12H23 M4,4L6,6 M18,18L20,20 M4,20L6,18 M18,6L20,4"},
        {"moon","M18,2C3,-1 -2,17 9,22C15,25 23,20 23,13C15,18 8,9 18,2Z"}
    };
    public static string[] Modules{get{return paths.Keys.Where(x=>x!="sun"&&x!="moon").ToArray();}}
    public static string PathData(string id){string value;if(!paths.TryGetValue(id,out value))throw new ArgumentException("Unknown navigation icon: "+id);return value;}
    public static DrawingGroup Draw(string id,Brush accent){var result=new DrawingGroup();var pen=new Pen(accent,1.65){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};using(var canvas=result.Open()){canvas.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,24,24));canvas.DrawGeometry(null,pen,Geometry.Parse(PathData(id)));}result.Freeze();return result;}
}
}
