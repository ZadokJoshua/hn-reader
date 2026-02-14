using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HNReader.Core.Models;

public class Interest : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _description = string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Creates an Interest with a name and an optional description.
    /// If no description is provided, defaults to "About {name}".
    /// </summary>
    public static Interest Create(string name, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Interest
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? $"About {name.Trim()}" : description.Trim()
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
