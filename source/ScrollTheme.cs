using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;

namespace Aurora {
public partial class MainWindow {
    void ApplyScrollTheme(){
        Application.Current.Resources["OuOScrollThumb"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?"#477A81":"#77A4AA"));
        Application.Current.Resources["OuOScrollHover"]=Accent;
        const string xaml=@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'>
          <Setter Property='Background' Value='Transparent'/><Setter Property='Width' Value='11'/><Setter Property='MinWidth' Value='11'/>
          <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'>
            <Grid Background='Transparent'><Track x:Name='PART_Track' Orientation='{TemplateBinding Orientation}' IsDirectionReversed='True'>
              <Track.DecreaseRepeatButton><RepeatButton x:Name='Decrease' Command='ScrollBar.PageUpCommand' Opacity='0' IsTabStop='False'/></Track.DecreaseRepeatButton>
              <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'>
                <Border x:Name='Pill' Background='{DynamicResource OuOScrollThumb}' CornerRadius='5' Margin='2'/>
                <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Pill' Property='Background' Value='{DynamicResource OuOScrollHover}'/></Trigger></ControlTemplate.Triggers>
              </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
              <Track.IncreaseRepeatButton><RepeatButton x:Name='Increase' Command='ScrollBar.PageDownCommand' Opacity='0' IsTabStop='False'/></Track.IncreaseRepeatButton>
            </Track></Grid>
            <ControlTemplate.Triggers><Trigger Property='Orientation' Value='Horizontal'><Setter TargetName='PART_Track' Property='IsDirectionReversed' Value='False'/><Setter TargetName='Decrease' Property='Command' Value='ScrollBar.PageLeftCommand'/><Setter TargetName='Increase' Property='Command' Value='ScrollBar.PageRightCommand'/></Trigger></ControlTemplate.Triggers>
          </ControlTemplate></Setter.Value></Setter>
          <Style.Triggers><Trigger Property='Orientation' Value='Horizontal'><Setter Property='Width' Value='Auto'/><Setter Property='Height' Value='11'/><Setter Property='MinWidth' Value='0'/></Trigger></Style.Triggers>
        </Style>";
        if(!Application.Current.Resources.Contains("OuOScrollStyle"))Application.Current.Resources["OuOScrollStyle"]=(Style)XamlReader.Parse(xaml);
        Application.Current.Resources[typeof(ScrollBar)]=Application.Current.Resources["OuOScrollStyle"];
    }
}
}
