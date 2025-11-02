using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TreeMethod.ViewModels
{
    public class GoalWeightViewModel : INotifyPropertyChanged
    {
        private int _weight = 1;
        private string _goalName;

        public GoalWeightViewModel(string goalName, int weight = 1)
        {
            GoalName = goalName;
            Weight = weight;
        }

        public string GoalName
        {
            get => _goalName;
            set
            {
                if (_goalName != value)
                {
                    _goalName = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Weight
        {
            get => _weight;
            set
            {
                if (_weight != value)
                {
                    _weight = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


