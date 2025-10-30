using System.ComponentModel;

namespace TreeMethod.Models
{
    public class MatrixRow : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private Dictionary<string, int> _values = new Dictionary<string, int>();

        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        // Свойство для прямого доступа к значениям без индексатора
        public Dictionary<string, int> Values
        {
            get { return _values; }
            set
            {
                _values = value;
                OnPropertyChanged(nameof(Values));
            }
        }

        public void SetValue(string key, int val)
        {
            if (_values.ContainsKey(key))
                _values[key] = val;
            else
                _values.Add(key, val);

            OnPropertyChanged(nameof(Values));
        }

        public int GetValue(string key)
        {
            if (_values.ContainsKey(key))
                return _values[key];
            return 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
