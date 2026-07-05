using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>Common base for Flammenwerfer view models. Frosty has no ObservableObject of its own.</summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>Raises PropertyChanged for every name given, for properties that share one input.</summary>
        protected void OnPropertiesChanged(params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
                OnPropertyChanged(propertyName);
        }

        /// <summary>Sets a field and raises PropertyChanged if it changed. Returns whether it changed.</summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
