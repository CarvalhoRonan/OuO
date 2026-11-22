using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Aurora {
public partial class MainWindow {
    void ApplyComboTheme(){
        Application.Current.Resources["OuOComboSurface"]=Surface;
        Application.Current.Resources["OuOComboBackground"]=Bg;
        Application.Current.Resources["OuOComboInk"]=Ink;
        Application.Current.Resources["OuOComboAccent"]=Accent;
        Application.Current.Resources["OuOComboSelectedInk"]=dark?Brushes.Black:Brushes.White;
        Application.Current.Resources["OuOComboBorder"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?"#344057":"#C8D3E3"));
        const string xaml=@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ComboBox}'>
          <Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/>
          <Setter Property='Background' Value='{DynamicResource OuOComboBackground}'/>
          <Setter Property='BorderBrush' Value='{DynamicResource OuOComboBorder}'/>
          <Setter Property='MinHeight' Value='39'/>
          <Setter Property='Padding' Value='11,7'/>
          <Setter Property='ItemContainerStyle'><Setter.Value><Style TargetType='{x:Type ComboBoxItem}'>
            <Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/><Setter Property='Padding' Value='11,8'/>
            <Setter Property='Background' Value='{DynamicResource OuOComboSurface}'/>
            <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBoxItem}'>
              <Border x:Name='ItemBorder' Background='{TemplateBinding Background}' CornerRadius='5' Margin='3,1' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border>
              <ControlTemplate.Triggers>
                <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='ItemBorder' Property='Background' Value='{DynamicResource OuOComboAccent}'/><Setter Property='Foreground' Value='{DynamicResource OuOComboSelectedInk}'/></Trigger>
                <Trigger Property='IsSelected' Value='True'><Setter TargetName='ItemBorder' Property='BorderBrush' Value='{DynamicResource OuOComboAccent}'/><Setter TargetName='ItemBorder' Property='BorderThickness' Value='1'/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style></Setter.Value></Setter>
          <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBox}'>
            <Grid>
              <ToggleButton x:Name='Toggle' Focusable='False' ClickMode='Press' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
                <ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'><Border x:Name='Frame' Background='{DynamicResource OuOComboBackground}' BorderBrush='{DynamicResource OuOComboBorder}' BorderThickness='1' CornerRadius='6' Padding='11,7'>
                  <DockPanel><Path DockPanel.Dock='Right' Width='9' Height='6' Margin='12,0,1,0' VerticalAlignment='Center' Fill='{DynamicResource OuOComboAccent}' Data='M 0 0 L 4.5 5 L 9 0 Z'/>
                    <ContentPresenter Content='{Binding SelectionBoxItem, RelativeSource={RelativeSource AncestorType=ComboBox}}' ContentTemplate='{Binding SelectionBoxItemTemplate, RelativeSource={RelativeSource AncestorType=ComboBox}}' VerticalAlignment='Center' TextElement.Foreground='{DynamicResource OuOComboInk}'/>
                  </DockPanel></Border>
                <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Frame' Property='BorderBrush' Value='{DynamicResource OuOComboAccent}'/></Trigger></ControlTemplate.Triggers>
                </ControlTemplate></ToggleButton.Template>
              </ToggleButton>
              <Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'>
                <Border Width='{Binding ActualWidth, ElementName=Toggle}' MaxHeight='300' Background='{DynamicResource OuOComboSurface}' BorderBrush='{DynamicResource OuOComboAccent}' BorderThickness='1' CornerRadius='6' Padding='3'>
                  <ScrollViewer VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled' CanContentScroll='True'><ItemsPresenter/></ScrollViewer>
                </Border>
              </Popup>
            </Grid>
            <ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.55'/></Trigger></ControlTemplate.Triggers>
          </ControlTemplate></Setter.Value></Setter>
        </Style>";
        Application.Current.Resources[typeof(ComboBox)]=(Style)XamlReader.Parse(xaml);
    }
}
}
