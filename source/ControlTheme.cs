using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
namespace Aurora {
public partial class MainWindow {
    void ApplyControlTheme(){
        Application.Current.Resources["OuOSelectionSurface"]=Color(dark?"#244254":"#DCECF3");
        const string xaml=@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
          <Style TargetType='{x:Type Expander}'>
            <Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/>
            <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Expander}'>
              <Grid><Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                <ToggleButton IsChecked='{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}' HorizontalContentAlignment='Stretch' Background='Transparent' Foreground='{TemplateBinding Foreground}' BorderThickness='0' Cursor='Hand'>
                  <ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'><Border Background='Transparent' Padding='2,5'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='20'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><Path x:Name='Arrow' Data='M 5,2 L 11,8 L 5,14' Stroke='{DynamicResource OuOComboAccent}' StrokeThickness='1.7' Width='14' Height='14' Stretch='Uniform' RenderTransformOrigin='0.5,0.5'><Path.RenderTransform><RotateTransform Angle='0'/></Path.RenderTransform></Path><ContentPresenter Grid.Column='1' Content='{TemplateBinding Content}' TextElement.Foreground='{TemplateBinding Foreground}' VerticalAlignment='Center'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Arrow' Property='RenderTransform'><Setter.Value><RotateTransform Angle='90'/></Setter.Value></Setter></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter Property='Opacity' Value='0.75'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
                  <ContentPresenter ContentSource='Header'/>
                </ToggleButton>
                <ContentPresenter x:Name='ExpandedContent' Grid.Row='1' Visibility='Collapsed' Margin='0,7,0,0' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'/>
              </Grid><ControlTemplate.Triggers><Trigger Property='IsExpanded' Value='True'><Setter TargetName='ExpandedContent' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType='{x:Type ContextMenu}'>
            <Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/><Setter Property='Background' Value='{DynamicResource OuOComboSurface}'/><Setter Property='BorderBrush' Value='{DynamicResource OuOComboBorder}'/><Setter Property='BorderThickness' Value='1'/>
            <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ContextMenu}'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7' Padding='4'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Cycle'/></Border></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType='{x:Type ToolTip}'>
            <Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/><Setter Property='Background' Value='{DynamicResource OuOComboSurface}'/><Setter Property='BorderBrush' Value='{DynamicResource OuOComboBorder}'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='12'/><Setter Property='MaxWidth' Value='460'/>
            <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ToolTip}'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='8' Padding='{TemplateBinding Padding}'><ContentPresenter TextElement.Foreground='{TemplateBinding Foreground}'/></Border></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType='{x:Type CheckBox}'>
            <Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='HorizontalContentAlignment' Value='Left'/>
            <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type CheckBox}'>
              <ControlTemplate.Resources><Style TargetType='{x:Type AccessText}'><Setter Property='TextWrapping' Value='Wrap'/></Style></ControlTemplate.Resources>
              <Grid Background='Transparent'><Grid.ColumnDefinitions><ColumnDefinition Width='23'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                <Border x:Name='Mark' Width='16' Height='16' CornerRadius='3' BorderThickness='1' BorderBrush='{DynamicResource OuOComboBorder}' Background='{DynamicResource OuOComboBackground}' VerticalAlignment='Top' Margin='0,2,6,0'>
                  <Grid><TextBlock x:Name='Tick' Text='✓' FontSize='12' FontWeight='Bold' Foreground='{DynamicResource OuOComboSelectedInk}' HorizontalAlignment='Center' VerticalAlignment='Center' Visibility='Collapsed'/><TextBlock x:Name='Mixed' Text='−' FontSize='12' Foreground='{DynamicResource OuOComboInk}' HorizontalAlignment='Center' VerticalAlignment='Center' Visibility='Collapsed'/></Grid>
                </Border><ContentPresenter Grid.Column='1' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' RecognizesAccessKey='True' TextElement.Foreground='{TemplateBinding Foreground}'/>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property='IsChecked' Value='True'><Setter TargetName='Tick' Property='Visibility' Value='Visible'/><Setter TargetName='Mark' Property='Background' Value='{DynamicResource OuOComboAccent}'/><Setter TargetName='Mark' Property='BorderBrush' Value='{DynamicResource OuOComboAccent}'/></Trigger>
                <Trigger Property='IsChecked' Value='{x:Null}'><Setter TargetName='Mixed' Property='Visibility' Value='Visible'/></Trigger>
                <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Mark' Property='BorderBrush' Value='{DynamicResource OuOComboAccent}'/></Trigger>
                <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Mark' Property='BorderBrush' Value='{DynamicResource OuOComboAccent}'/><Setter TargetName='Mark' Property='BorderThickness' Value='2'/></Trigger>
                <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType='{x:Type ListBoxItem}'>
            <Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/><Setter Property='Background' Value='{DynamicResource OuOComboSurface}'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Padding' Value='3'/>
            <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ListBoxItem}'><Border x:Name='Row' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}' BorderThickness='1' BorderBrush='Transparent' CornerRadius='4'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'/></Border><ControlTemplate.Triggers>
              <Trigger Property='IsSelected' Value='True'><Setter TargetName='Row' Property='Background' Value='{DynamicResource OuOSelectionSurface}'/><Setter TargetName='Row' Property='BorderBrush' Value='{DynamicResource OuOComboAccent}'/></Trigger>
              <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Row' Property='BorderBrush' Value='{DynamicResource OuOComboBorder}'/></Trigger>
            </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType='{x:Type DataGrid}'>
            <Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/><Setter Property='RowBackground' Value='{DynamicResource OuOComboSurface}'/><Setter Property='AlternatingRowBackground' Value='{DynamicResource OuOComboBackground}'/><Setter Property='GridLinesVisibility' Value='None'/><Setter Property='BorderBrush' Value='{DynamicResource OuOComboBorder}'/>
          </Style>
          <Style TargetType='{x:Type DataGridCell}'>
            <Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='6'/><Style.Triggers><Trigger Property='IsSelected' Value='True'><Setter Property='Background' Value='{DynamicResource OuOSelectionSurface}'/><Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/></Trigger></Style.Triggers>
          </Style>
          <Style TargetType='{x:Type DataGridColumnHeader}'>
            <Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/><Setter Property='Background' Value='{DynamicResource OuOComboSurface}'/><Setter Property='Padding' Value='7,9'/><Setter Property='BorderBrush' Value='{DynamicResource OuOComboBorder}'/><Setter Property='BorderThickness' Value='0,0,0,1'/>
          </Style>
          <Style TargetType='{x:Type MenuItem}'>
            <Setter Property='Foreground' Value='{DynamicResource OuOComboInk}'/><Setter Property='Background' Value='{DynamicResource OuOComboSurface}'/><Setter Property='Padding' Value='12,8'/>
            <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type MenuItem}'><Border x:Name='Row' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}' CornerRadius='4'><ContentPresenter ContentSource='Header' RecognizesAccessKey='True'/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Row' Property='Background' Value='{DynamicResource OuOSelectionSurface}'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
        </ResourceDictionary>";
        var resources=(ResourceDictionary)XamlReader.Parse(xaml);foreach(object key in resources.Keys)Application.Current.Resources[key]=resources[key];
    }
}
}
