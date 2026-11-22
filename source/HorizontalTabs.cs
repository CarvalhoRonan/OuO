using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
namespace Aurora {
public partial class MainWindow {
    void ApplyTabTheme(){
        Application.Current.Resources["OuOTabInactive"]=Color(dark?"#28364B":"#E4ECF7");
        const string xaml=@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type Button}'>
          <Setter Property='Background' Value='{DynamicResource OuOTabInactive}'/><Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/>
          <Setter Property='BorderBrush' Value='{DynamicResource OuOComboBorder}'/><Setter Property='BorderThickness' Value='1,1,1,3'/><Setter Property='FontWeight' Value='SemiBold'/><Setter Property='MinHeight' Value='38'/>
          <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'><Border x:Name='Frame' CornerRadius='7' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextElement.Foreground='{TemplateBinding Foreground}'/></Border>
            <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Frame' Property='BorderBrush' Value='{DynamicResource OuOComboAccent}'/><Setter TargetName='Frame' Property='Opacity' Value='0.9'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Frame' Property='BorderBrush' Value='{DynamicResource OuOComboAccent}'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Frame' Property='Opacity' Value='0.72'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='Frame' Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers>
          </ControlTemplate></Setter.Value></Setter>
          <Style.Triggers><Trigger Property='Tag' Value='OuO.Tab.Selected'><Setter Property='Background' Value='{DynamicResource OuOComboAccent}'/><Setter Property='BorderBrush' Value='{DynamicResource OuOComboAccent}'/><Setter Property='Foreground' Value='{DynamicResource OuOComboSelectedInk}'/></Trigger></Style.Triggers>
        </Style>";
        Application.Current.Resources["OuO.HorizontalTab"]=(Style)XamlReader.Parse(xaml);
    }
    Button TabButton(string label,Action click,bool selected=false){var button=Button(label,click);button.ClearValue(Control.BackgroundProperty);button.ClearValue(Control.ForegroundProperty);button.ClearValue(Control.BorderBrushProperty);button.ClearValue(Control.BorderThicknessProperty);button.Style=(Style)Application.Current.Resources["OuO.HorizontalTab"];SelectTab(button,selected);return button;}
    void SelectTab(Button button,bool selected){button.Tag=selected?"OuO.Tab.Selected":"OuO.Tab";System.Windows.Automation.AutomationProperties.SetItemStatus(button,selected?T("Aba selecionada","Selected tab"):T("Aba disponível","Available tab"));}
}
}
