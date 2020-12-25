using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("Phone Rename", "Bazz3l", "0.0.5")]
    [Description("Ability to rename naughty named phones and log changes to discord.")]
    public class PhoneRename : CovalencePlugin
    {
        #region Fields

        private const string PermUse = "phonerename.use";

        private readonly Dictionary<string, string> _headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };
        
        private PluginConfig _pluginConfig;

        #endregion
        
        #region Config

        protected override void LoadDefaultConfig() => _pluginConfig = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _pluginConfig = Config.ReadObject<PluginConfig>();

                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                LoadDefaultConfig();
                
                SaveConfig();

                PrintError("Config file contains an error and has been replaced with the default file.");
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_pluginConfig, true);
        
        private class PluginConfig
        {
            [JsonProperty("DiscordWebhook (discord webhook url here)")]
            public string DiscordWebhook = "https://discord.com/api/webhooks/webhook-here";

            [JsonProperty("DiscordColor (discord embed color)")]
            public int DiscordColor = 65535;

            [JsonProperty("DiscordAuthor (discord embed author name)")]
            public string DiscordAuthor = "Phone Rename";
            
            [JsonProperty("DiscordAuthorImageUrl (discord embed author image url)")]
            public string DiscordAuthorImageUrl = "https://assets.umod.org/images/icons/plugin/5fa92b3f428d1.png";
            
            [JsonProperty("DiscordAuthorUrl (discord embed author url)")]
            public string DiscordAuthorUrl = "https://umod.org/users/bazz3l";
            
            [JsonProperty("LogToDiscord (log updated phone names to a discord channel)")]
            public bool LogToDiscord;

            [JsonProperty("WordList (list of ad words)")]
            public List<string> WordList = new List<string>
            {
                "fucker",
                "fuck",
                "cunt",
                "twat",
                "wanker",
                "bastard"
            };
        }

        #endregion
        
        #region Oxide

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                { "NoPermission", "No permission" },
                { "InvalidSyntax", "Invalid syntax, renamephone <phone-number> <new-name>" },
                { "NotFound", "No telephone found by that phone number." },
                { "Updated", "Phone was updated to {0}." },
                { "PhoneNumber", "Phone Number" },
                { "PhoneName", "Phone Name" },
                { "Connect", "Connect" },
                { "Server", "Server" },
                { "Profile", "Profile" }
            }, this);
        }
        
        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
        }

        private object OnPhoneNameUpdate(PhoneController phoneController, string phoneName, BasePlayer player) => UpdatePhoneName(player.IPlayer, phoneController, phoneName);

        #endregion
        
        #region Core

        private object UpdatePhoneName(IPlayer player, PhoneController phoneController, string phoneName)
        {
            phoneController.PhoneName = FilterWord(phoneName);

            if (_pluginConfig.LogToDiscord)
            {
                SendDiscordMessage(player, phoneController.PhoneName, phoneController.PhoneNumber.ToString());
            }

            return null;
        }
        
        private string FilterWord(string phoneName)
        {
            foreach (string filteredWord in _pluginConfig.WordList)
            {
                string strReplace = "";
                
                for (int i = 0; i <= filteredWord.Length; i++)
                {
                    strReplace += "*";
                }
                
                phoneName = Regex.Replace(phoneName, filteredWord, strReplace, RegexOptions.IgnoreCase);
            }
            
            return phoneName;
        }
        
        private Telephone FindByPhoneNumber(int phoneNumber)
        {
            foreach (Telephone telephone in BaseNetworkable.serverEntities.OfType<Telephone>())
            {
                if (telephone.Controller != null && telephone.Controller.PhoneNumber == phoneNumber)
                {
                    return telephone;
                }
            }

            return null;
        }

        #endregion

        #region Command

        [Command("renamephone")]
        private void RenamePhoneCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PermUse))
            {
                player.Message(Lang("NoPermission", player.Id));
                return;
            }
            
            if (args.Length < 2)
            {
                player.Message(Lang("InvalidSyntax", player.Id));
                return;
            }
            
            int phoneNumber;
            
            if (!int.TryParse(args[0], out phoneNumber))
            {
                player.Message(Lang("InvalidSyntax", player.Id));
                return;
            }
            
            Telephone telephone = FindByPhoneNumber(phoneNumber);
            
            if (telephone == null)
            {
                player.Message(Lang("NotFound", player.Id));
                return;
            }

            string phoneName = string.Join(" ", args.Skip(1).ToArray());

            UpdatePhoneName(player, telephone.Controller, phoneName);
            
            player.Message(Lang("Updated", player.Id, phoneName));
        }

        #endregion

        #region Discord

        private void SendDiscordMessage(IPlayer player, string phoneName, string phoneNumber)
        {
            Embed embed = new Embed
            {
                Color = _pluginConfig.DiscordColor,
                Author = new Author
                {
                    Name = _pluginConfig.DiscordAuthor,
                    Url = _pluginConfig.DiscordAuthorUrl,
                    IconUrl = _pluginConfig.DiscordAuthorImageUrl,
                },
                Fields = new List<Field>
                {
                    new Field(Lang("Server"), ConVar.Server.hostname, false),
                    new Field(Lang("PhoneNumber"), phoneNumber, false),
                    new Field(Lang("PhoneName"), phoneName, false),
                    new Field(Lang("Profile"), $"[{player.Name}](https://steamcommunity.com/profiles/{player.Id})", false),
                    new Field(Lang("Connect"), $"steam://connect/{covalence.Server.Address}:{covalence.Server.Port}", false),
                }
            };
            
            webrequest.Enqueue(_pluginConfig.DiscordWebhook, new DiscordMessage("", new List<Embed> { embed }).ToJson(), (code, response) => {}, this, RequestMethod.POST, _headers);
        }

        private class DiscordMessage
        {
            public DiscordMessage(string content, List<Embed> embeds)
            {
                Content = content;
                Embeds = embeds;
            }
            
            [JsonProperty("content")]
            public string Content { get; set; }

            [JsonProperty("embeds")]
            public List<Embed> Embeds { get; set; }

            public string ToJson() => JsonConvert.SerializeObject(this);
        }

        private class Embed
        {
            [JsonProperty("author")]
            public Author Author { get; set; }
            
            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("color")]
            public int Color { get; set; }

            [JsonProperty("fields")]
            public List<Field> Fields { get; set; } = new List<Field>();
        }

        private class Author
        {
            [JsonProperty("icon_url")]
            public string IconUrl  { get; set; }
            
            [JsonProperty("name")]
            public string Name  { get; set; }
            
            [JsonProperty("url")]
            public string Url  { get; set; }
        }

        private class Field
        {
            public Field(string name, string value, bool inline)
            {
                Name = name;
                Value = value;
                Inline = inline;
            }

            [JsonProperty("name")] 
            public string Name { get; set; }

            [JsonProperty("value")] 
            public string Value { get; set; }

            [JsonProperty("inline")] 
            public bool Inline { get; set; }
        }

        #endregion
        
        #region Helpers

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}