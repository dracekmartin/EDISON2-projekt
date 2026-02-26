using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace EDControls
{
    public partial class FormInput : UserControl
    {
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(
                nameof(Items),
                typeof(IEnumerable<FormItem>),
                typeof(FormInput),
                new PropertyMetadata(null));

        public IEnumerable<FormItem>? Items
        {
            get => (IEnumerable<FormItem>?)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public FormInput()
        {
            InitializeComponent();
        }
    }
}
