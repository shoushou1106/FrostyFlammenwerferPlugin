using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FsLocalizationPlugin.ViewModels
{
    /// <summary>
    /// Common base view models. Frosty does not ship an
    /// ObservableObject/ViewModelBase of its own, so this is the minimal one we need:
    /// property-changed notification plus a couple of small helpers to cut down on
    /// boilerplate in the view models that used to live directly in window code-behind.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises <see cref="PropertyChanged"/> for the given property, or for the calling
        /// member when <paramref name="propertyName"/> is omitted.
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Raises <see cref="PropertyChanged"/> for every property name given. Several
        /// Flammenwerfer view models expose a handful of properties that are all derived
        /// from one piece of input state (e.g. a hash/ID text box driving a preview, a
        /// hex conversion, and a "can I actually do this" flag) - this lets the setter for
        /// that input fan the notification out in one call instead of one per property.
        /// </summary>
        protected void OnPropertiesChanged(params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
                OnPropertyChanged(propertyName);
        }

        /// <summary>
        /// Sets <paramref name="field"/> to <paramref name="value"/> and raises
        /// <see cref="PropertyChanged"/> if the value actually changed. Returns
        /// <see langword="true"/> when the value changed, so callers can chain
        /// additional side effects only when needed.
        /// </summary>
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
