using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EDControls
{
    public class FormField : FormItem, INotifyPropertyChanged
    {
        public string Label { get; set; } = "";

        public FieldType FieldType { get; set; } = FieldType.Text;

        private string? _value;
        public string? Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
