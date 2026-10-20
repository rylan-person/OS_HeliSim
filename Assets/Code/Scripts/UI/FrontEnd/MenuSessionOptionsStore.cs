using System;
using UnityEngine;

namespace HeliSim.FrontEnd
{
    public sealed class MenuSessionOptionsStore
    {
        public const string PreferenceKey = "HeliSim.MainMenu.SessionOptions.v1";
        private const int CurrentVersion = 1;

        [Serializable]
        private sealed class Envelope
        {
            public int Version;
            public MenuSessionOptions Options;
        }

        public MenuSessionOptions Load()
        {
            if (!PlayerPrefs.HasKey(PreferenceKey))
            {
                return new MenuSessionOptions();
            }
            try
            {
                Envelope envelope = JsonUtility.FromJson<Envelope>(PlayerPrefs.GetString(PreferenceKey));
                if (envelope != null && envelope.Version == CurrentVersion && envelope.Options != null &&
                    envelope.Options.TryValidate(MenuSessionOptions.IsSupportedScene, out _))
                {
                    return envelope.Options;
                }
            }
            catch (ArgumentException)
            {
                // Preserve the stored value so corruption can be inspected or replaced by a valid save.
            }
            return new MenuSessionOptions();
        }

        public void Save(MenuSessionOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            MenuSessionOptions validated = options.Clone();
            if (!validated.TryValidate(MenuSessionOptions.IsSupportedScene, out string error))
            {
                throw new ArgumentException(error, nameof(options));
            }
            var envelope = new Envelope { Version = CurrentVersion, Options = validated };
            PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(envelope));
            PlayerPrefs.Save();
        }
    }
}
