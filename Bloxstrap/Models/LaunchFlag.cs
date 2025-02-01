using System;

namespace Hellstrap.Models
{
    public class LaunchFlag
    {
        // Auto-implemented property with private set to ensure it cannot be modified externally.
        public string Identifiers { get; }

        // Backing field for Active property to allow property-style access.
        private bool _active;

        // Publicly accessible property for Active status with getter/setter logic.
        public bool Active
        {
            get => _active;
            set => _active = value;  // Now public to allow external modification.
        }

        // Nullable Data property, default is null.
        public string? Data { get; set; }

        // Constructor to initialize Identifiers with a check for null values.
        public LaunchFlag(string identifiers)
        {
            Identifiers = identifiers ?? throw new ArgumentNullException(nameof(identifiers));
            _active = false; // Default value for Active.
        }

        // Methods to modify the Active status directly.
        public void Activate() => Active = true;
        public void Deactivate() => Active = false;

        // Optional: Add a method to toggle Active state, making it more flexible.
        public void Toggle() => Active = !Active;
    }
}
