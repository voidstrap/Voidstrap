using System;

namespace Hellstrap.Models
{
    public class LaunchFlag
    {
        // Auto-implemented read-only property to prevent external modification
        public string Identifiers { get; }

        // Backing field for Active property
        private bool _active;

        // Public property with a getter and setter
        public bool Active
        {
            get => _active;
            set => _active = value;
        }

        // Nullable property for optional data
        public string? Data { get; set; }

        // Constructor with null check for identifiers
        public LaunchFlag(string identifiers)
        {
            Identifiers = identifiers ?? throw new ArgumentNullException(nameof(identifiers));
            _active = false;
        }

        // Methods to manage Active status
        public void Activate() => _active = true;
        public void Deactivate() => _active = false;
        public void Toggle() => _active = !_active;
    }
}