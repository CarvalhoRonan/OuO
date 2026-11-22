using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Aurora {
public partial class MainWindow {
    void ApplyButtonTheme(){
        const string xaml=@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type Button}'>
          <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'>
            <Border x:Name='Frame' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='5' Padding='{TemplateBinding Padding}'>
              <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' RecognizesAccessKey='True' TextElement.Foreground='{TemplateBinding Foreground}'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Frame' Property='BorderBrush' Value='{DynamicResource OuOComboAccent}'/></Trigger>
              <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Frame' Property='BorderBrush' Value='{DynamicResource OuOComboAccent}'/><Setter TargetName='Frame' Property='BorderThickness' Value='2'/></Trigger>
              <Trigger Property='IsPressed' Value='True'><Setter TargetName='Frame' Property='Opacity' Value='0.76'/></Trigger>
              <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Frame' Property='Opacity' Value='0.45'/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate></Setter.Value></Setter>
        </Style>";
        Application.Current.Resources[typeof(Button)]=(Style)XamlReader.Parse(xaml);
    }
}
}
