using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal static class CharacterRuntime
    {
        internal const string UpdateNote = "CompanionLab updates are manual. Upstream Capri installers are disabled for this fork.";
        internal static CharacterCatalog Catalog;
        internal static void Open(string data)
        {
            Catalog = new CharacterCatalog(Path.Combine(data, "characters"), Assembly.GetExecutingAssembly().GetManifestResourceStream);
            Catalog.Restore();
        }

        internal static Stream OpenArt(string name)
        {
            CharacterProfile profile = Catalog == null ? null : Catalog.Current;
            Stream image = profile == null ? null : profile.OpenSprite(name);
            return image ?? Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        }
    }

    [DataContract]
    internal sealed class CharacterVoiceState
    {
        [DataMember] public bool Quiet, Silent, Hd, Server;
        [DataMember] public string Style, VoiceId, VoiceModel;
        [DataMember] public int Pitch, Speed;

        internal static CharacterVoiceState Capture(Preferences settings)
        {
            return new CharacterVoiceState { Quiet = settings.Quiet, Silent = settings.Silent, Hd = settings.HdVoice,
                Server = settings.ServerVoice, Style = settings.VoiceStyle, VoiceId = settings.VoiceId, VoiceModel = settings.VoiceModel,
                Pitch = settings.VoicePitch, Speed = settings.VoiceSpeed };
        }

        internal void Apply(Preferences settings)
        {
            settings.Quiet = Quiet; settings.Silent = Silent; settings.HdVoice = Hd; settings.ServerVoice = Server;
            settings.VoiceStyle = Style; settings.VoiceId = VoiceId; settings.VoiceModel = VoiceModel;
            settings.VoicePitch = Math.Max(-12, Math.Min(12, Pitch)); settings.VoiceSpeed = Math.Max(50, Math.Min(200, Speed));
        }
    }

    internal sealed partial class Companion
    {
        private ToolStripMenuItem charactersMenu;
        private string appliedCharacter;

        private void AddCharactersMenu()
        {
            charactersMenu = new ToolStripMenuItem("Characters");
            charactersMenu.DropDownOpening += delegate { RefreshCharactersMenu(); };
            ToolStripDropDownMenu drop = (ToolStripDropDownMenu)charactersMenu.DropDown;
            drop.Renderer = menu.Renderer; drop.ForeColor = menu.ForeColor; drop.BackColor = menu.BackColor;
            drop.Font = menu.Font; drop.ShowImageMargin = false; drop.ShowCheckMargin = true;
            menu.Items.Add(charactersMenu);
        }

        private void RefreshCharactersMenu()
        {
            charactersMenu.DropDownItems.Clear();
            CharacterCatalog catalog = CharacterRuntime.Catalog;
            if (catalog == null) return;
            foreach (CharacterProfile profile in catalog.Scan())
            {
                CharacterProfile selected = profile;
                ToolStripMenuItem item = new ToolStripMenuItem(profile.Name, null, delegate
                {
                    string reason;
                    if (!ChangeCharacter(selected, out reason)) Notice.Tell(this, reason, "CompanionLab character", false);
                });
                item.Checked = catalog.Current.Id == profile.Id;
                charactersMenu.DropDownItems.Add(item);
            }
            charactersMenu.DropDownItems.Add(new ToolStripSeparator());
            charactersMenu.DropDownItems.Add(new ToolStripMenuItem("Open character folder", null, delegate
            {
                try { Directory.CreateDirectory(catalog.Root); Process.Start(new ProcessStartInfo(catalog.Root) { UseShellExecute = true }); }
                catch (Exception error) { Notice.Tell(this, error.Message, "CompanionLab characters", true); }
            }));
            if (catalog.Errors.Count > 0)
            {
                string errors = string.Join(Environment.NewLine, catalog.Errors.ToArray());
                charactersMenu.DropDownItems.Add(new ToolStripMenuItem("Profile errors (" + catalog.Errors.Count + ")", null,
                    delegate { Notice.Tell(this, errors, "CompanionLab characters", true); }));
            }
        }

        private void SetUpCharacters()
        {
            if (preferences.CharacterVoices == null) preferences.CharacterVoices = new Dictionary<string, CharacterVoiceState>();
            string owner = preferences.CharacterVoiceOwner ?? "capri";
            preferences.CharacterVoices[owner] = CharacterVoiceState.Capture(preferences);
            if (!preferences.CharacterVoices.ContainsKey("capri")) preferences.CharacterVoices["capri"] = CharacterVoiceState.Capture(Preferences.Fresh());
            ApplyCharacter(CharacterRuntime.Catalog.Current);
            foreach (string error in CharacterRuntime.Catalog.Errors) Brain.Log("character profile", -1, error);
        }

        private bool CharacterBusy
        {
            get { return working != null || job != null || chatting || speech != null || speaker.Busy || ears.Listening || talkState == TalkState.Thinking || !double.IsNaN(thinkingSince)
                || settingUpHd || dragging || pressed || cloneWindow != null; }
        }

        private bool ChangeCharacter(CharacterProfile next, out string reason)
        {
            reason = null;
            if (CharacterBusy) { reason = "Finish or stop the current conversation, speech or task before changing character."; return false; }
            CharacterCatalog catalog = CharacterRuntime.Catalog;
            CharacterProfile previous = catalog.Current;
            CharacterVoiceState voiceWas = CharacterVoiceState.Capture(preferences);
            try
            {
                catalog.Select(next);
                preferences.CharacterVoices[previous.Id] = voiceWas;
                ApplyCharacter(catalog.Current);
                Clip.ResetAll();
                Bitmap oldFace = Takeover.Face;
                Takeover.Face = LoadArt("Capri.Sprite.face.png");
                if (oldFace != null) oldFace.Dispose();
                RenderFrame();
                SavePreferences();
                ActionJournal.Record("character", "selected:" + catalog.Current.Id);
                return true;
            }
            catch (Exception error)
            {
                if (!(error is IOException) && !(error is InvalidDataException) && !(error is UnauthorizedAccessException) && !(error is ArgumentException) && !(error is System.Runtime.InteropServices.ExternalException)) throw;
                try
                {
                    catalog.RestoreSelection(previous);
                    voiceWas.Apply(preferences);
                    preferences.CharacterVoices[previous.Id] = voiceWas;
                    ApplyCharacter(previous);
                    Clip.ResetAll();
                    RenderFrame();
                }
                catch (Exception rollback) { Brain.Log("character rollback", -1, rollback.Message); }
                reason = "Character was not changed: " + error.Message;
                Brain.Log("character profile", -1, error.Message);
                return false;
            }
        }

        private void ApplyCharacter(CharacterProfile profile)
        {
            CharacterVoiceState saved;
            if (preferences.CharacterVoices.TryGetValue(profile.Id, out saved)) saved.Apply(preferences);
            else
            {
                preferences.CharacterVoices["capri"].Apply(preferences);
                CharacterVoice config = profile.Voice;
                if (config.Mode == "off") preferences.Silent = true;
                else if (config.Mode == "local")
                {
                    preferences.Quiet = true; preferences.Silent = false;
                    preferences.HdVoice = preferences.ServerVoice = false;
                    preferences.VoiceStyle = config.Style;
                    preferences.VoicePitch = config.Pitch; preferences.VoiceSpeed = config.Speed;
                }
            }
            if (!preferences.HdVoice) { qwen.Cancel = true; qwen.Stop(); }
            kokoro.Style = LocalVoice.Find(preferences.VoiceStyle);
            voice.VoiceId = preferences.VoiceId; voice.Model = preferences.VoiceModel;
            VoiceFx.Pitch = preferences.VoicePitch; VoiceFx.Speed = preferences.VoiceSpeed;
            string data = Path.GetDirectoryName(settingsPath);
            string personality = profile.Id == "capri" ? Path.Combine(data, Brain.PersonalityName) : Path.Combine(Path.Combine(Path.Combine(data, "character-settings"), profile.Id), Brain.PersonalityName);
            Directory.CreateDirectory(Path.GetDirectoryName(personality));
            Brain.UseCharacter(profile.Name, profile.Personality, personality);
            appliedCharacter = profile.Id;
            preferences.CharacterVoiceOwner = profile.Id;
            Text = "CompanionLab - " + profile.Name;
            AccessibleName = profile.Name + " desktop companion";
            tray.Text = Text;
            charactersMenu.Text = "Characters (" + profile.Name + ")";
            ShowVoice();
            mainframe.Sync();
        }

        internal static void ConservativePermissions(Preferences settings)
        {
            if (settings.Permissions != null && settings.Permissions.Length == 10 && settings.Permissions.Trim('0', '1').Length == 0) return;
            settings.Permissions = Security.Of(Security.Guarded);
            settings.Bypass = settings.HeyCapri = false;
        }
    }
}
