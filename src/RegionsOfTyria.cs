using Blish_HUD;
using Blish_HUD.Extended;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Nekres.Regions_Of_Tyria.Core.Services;
using Nekres.Regions_Of_Tyria.Geometry;
using Nekres.Regions_Of_Tyria.UI.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace Nekres.Regions_Of_Tyria {

    [Export(typeof(Module))]
    public class RegionsOfTyria : Module {

        internal static readonly Logger Logger = Logger.GetLogger(typeof(RegionsOfTyria));

        internal static RegionsOfTyria Instance;

        #region Service Managers

        internal SettingsManager    SettingsManager    => ModuleParameters.SettingsManager;
        internal ContentsManager    ContentsManager    => ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager      Gw2ApiManager      => ModuleParameters.Gw2ApiManager;

        #endregion

        private SettingEntry<float> _showDuration;
        private SettingEntry<float> _fadeInDuration;
        private SettingEntry<float> _fadeOutDuration;
        private SettingEntry<float> _effectDuration;
        private SettingEntry<bool>  _toggleMapNotification;
        private SettingEntry<bool>  _toggleSectorNotification;
        private SettingEntry<bool>  _includeRegionInMapNotification;
        private SettingEntry<bool>  _includeMapInSectorNotification;
        private SettingEntry<bool>  _showSectorOnCompass;
        private SettingEntry<bool>  _hideInCombat;

        internal SettingEntry<bool>  Translate;
        internal SettingEntry<bool>  Dissolve;
        internal SettingEntry<bool>  UnderlineHeader;
        internal SettingEntry<bool>  OverlapHeader;
        internal SettingEntry<float> VerticalPosition;
        internal SettingEntry<float> FontSize;
        internal SettingEntry<float> RevealVolume;
        internal SettingEntry<float> VanishVolume;
        internal SettingEntry<bool>  MuteReveal;
        internal SettingEntry<bool>  MuteVanish;

        private  AsyncCache<int, Map>          _mapRepository;
        private  AsyncCache<int, List<Sector>> _sectorRepository;

        internal SoundEffect DecodeSound;
        internal SoundEffect VanishSound;
        internal Effect      DissolveEffect;
        internal BitmapFont  KrytanFont;
        internal BitmapFont  KrytanFontSmall;
        internal BitmapFont  TitlingFont;
        internal BitmapFont  TitlingFontSmall;

        private Map    _currentMap     = new();
        private Sector _currentSector  = Sector.Zero;
        private Sector _previousSector = Sector.Zero;

        private DateTime              _lastIndicatorChange = DateTime.UtcNow;
        private NotificationIndicator _notificationIndicator;

        private double   _lastRun;
        private DateTime _lastUpdate = DateTime.UtcNow;
        
        private bool _unloading;

        private string _lastSectorName;
        internal CompassService Compass;

        [ImportingConstructor]
        public RegionsOfTyria([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) {
            Instance = this;
        }

        protected override void DefineSettings(SettingCollection settings) {
            var generalCol = settings.AddSubCollection("general", true, () => "General");

            Translate = generalCol.DefineSetting("translate", true,
                                                        () => "Translate from New Krytan",
                                                        () => "Makes zone notifications appear in New Krytan before they are revealed to you.");

            Dissolve = generalCol.DefineSetting("dissolve", true, 
                                                () => "Dissolve when Fading Out", 
                                                () => "Makes zone notifications burn up when they fade out.");

            UnderlineHeader = generalCol.DefineSetting("underline_heading", true, 
                                                       () => "Underline Heading", 
                                                       () => "Underlines the top text if a notification has one.");

            OverlapHeader = generalCol.DefineSetting("overlap_heading", false, 
                                                      () => "Overlap Heading", 
                                                      () => "Makes the bottom text stylishly overlap the top text.");

            VerticalPosition = generalCol.DefineSetting("pos_y", 25f,
                                                        () => "Vertical Position",
                                                        () => "Sets the vertical position of area notifications.");

            FontSize = generalCol.DefineSetting("font_size", 76f, 
                                                        () => "Font Size", 
                                                        () => "Sets the size of the zone notification text.");

            _hideInCombat = generalCol.DefineSetting("hide_if_combat", true, 
                                                     () => "Disable during Combat", 
                                                     () => "Disables zone notifications during combat.");

            var soundCol    = settings.AddSubCollection("sound",    true, () => "Sound");
            RevealVolume = soundCol.DefineSetting("reveal_vol", 70f,
                                                  () => "Reveal Volume",
                                                  () => "Sets the reveal sound volume.");

            VanishVolume = soundCol.DefineSetting("vanish_vol", 50f,
                                                  () => "Vanish Volume", 
                                                  () => "Sets the vanish sound volume.");

            MuteReveal = soundCol.DefineSetting("mute_reveal", false,
                                                () => "Mute Reveal Sound",
                                                () => "Mutes the sound effect which plays during reveal.");

            MuteVanish = soundCol.DefineSetting("mute_vanish", false,
                                                  () => "Mute Vanish Sound",
                                                  () => "Mutes the sound effect which plays during fade-out.");

            var durationCol = settings.AddSubCollection("durations", true, () => "Durations");

            _showDuration = durationCol.DefineSetting("show", 80f,
                                                             () => "Show Duration",
                                                             () => "The duration in which to stay in full opacity.");

            _fadeInDuration = durationCol.DefineSetting("fade_in", 45f,
                                                               () => "Fade-In Duration",
                                                               () => "The duration of the fade-in.");

            _fadeOutDuration = durationCol.DefineSetting("fade_out", 65f,
                                                                () => "Fade-Out Duration",
                                                                () => "The duration of the fade-out.");

            _effectDuration = durationCol.DefineSetting("effect", 30f,
                                                               () => "Reveal Effect Duration",
                                                               () => "The duration of the reveal or translation effect.");

            var mapCol = settings.AddSubCollection("map_alert", true, () => "Map Notification");

            _toggleMapNotification = mapCol.DefineSetting("enabled", true,
                                                                    () => "Enabled",
                                                                    () => "Shows a map's name after entering it.");

            _includeRegionInMapNotification = mapCol.DefineSetting("prefix_region", true,
                                                                          () => "Include Region",
                                                                          () => "Shows the region's name above the map's name.");

            

            var sectorCol = settings.AddSubCollection("sector_alert", true, () => "Sector Notification");

            _toggleSectorNotification = sectorCol.DefineSetting("enabled", true,
                                                                       () => "Enabled",
                                                                       () => "Shows a sector's name after entering.");
            _includeMapInSectorNotification = sectorCol.DefineSetting("prefix_map", true,
                                                                      () => "Include Map",
                                                                      () => "Shows the map's name above the sector's name.");
            _showSectorOnCompass = sectorCol.DefineSetting("compass", true, 
                                                           () => "Show Sector on Compass", 
                                                           () => "Shows a sector's name at the top of your compass (ie. minimap).");
        }

        protected override void Initialize() {
            _mapRepository    = new AsyncCache<int, Map>(RequestMap);
            _sectorRepository = new AsyncCache<int, List<Sector>>(RequestSectors);

            DissolveEffect = ContentsManager.GetEffect("effects/dissolve.mgfx");
            DecodeSound = ContentsManager.GetSound("sounds/decode.wav");
            VanishSound = ContentsManager.GetSound("sounds/vanish.wav");

            Compass = new CompassService();
        }

        protected override async void Update(GameTime gameTime) {
            if (_currentMap.Id == 0) {
                return;
            }

            var sector = await GetSector(_currentMap);
            
            if (sector == Sector.Zero) {
                return;
            }

            // Module is unloading. Stop.
            if (_unloading) {
                return;
            }

            _lastSectorName = MapNotification.FilterDisplayName(sector.Name);
            if (_showSectorOnCompass.Value) {
                Compass?.Show(_lastSectorName);
            }

            // Still in the same sector. Ignore.
            if (_currentSector.Id == sector.Id) {
                return;
            }

            // Ignore back and forth between same two sectors.
            if (_previousSector.Id == sector.Id) {
                return;
            }

            _previousSector = _currentSector; // Move current sector to previous sector.
            _currentSector  = sector;         // Save new sector id as current sector id.

            var playerSpeed = GameService.Gw2Mumble.PlayerCharacter.GetSpeed(gameTime);

            if (DateTime.UtcNow.Subtract(_lastIndicatorChange).TotalMilliseconds > 250 && _notificationIndicator != null) {
                _notificationIndicator.Dispose();
                _notificationIndicator = null;
            }

            // Pause when Gw2Mumble is inactive or the player is not in-game
            if (!GameService.Gw2Mumble.IsAvailable || !GameService.GameIntegration.Gw2Instance.IsInGame) {
                return;
            }

            // Pause when sector notifications are disabled
            if (!_toggleSectorNotification.Value) {
                return;
            }

            // Pause when the player is moving too fast between zones to avoid spam
            if (playerSpeed > 54) {
                return;
            }

            // Pause when the player is in combat
            if (_hideInCombat.Value && GameService.Gw2Mumble.PlayerCharacter.IsInCombat) {
                return;
            }

            // Rate limit update
            if (gameTime.TotalGameTime.TotalMilliseconds - _lastRun < 10) {
                return;
            }

            // Cooldown to avoid spam
            if (DateTime.UtcNow.Subtract(_lastUpdate).TotalSeconds < 5) {
                return;
            }

            _lastRun    = gameTime.ElapsedGameTime.TotalMilliseconds;
            _lastUpdate = DateTime.UtcNow;

            ShowNotification(_includeMapInSectorNotification.Value ? _currentMap.Name : null, sector.Name);
        }

        private async Task<string> GetTrueMapName(Map map) {
            // Some maps consist of just a single sector and hide their actual name in it.
            var sectors = await _sectorRepository.GetItem(GameService.Gw2Mumble.CurrentMap.Id);
            if (sectors == null || sectors.Count is < 1 or > 1) {
                return map.Name;
            }
            var sector = sectors.First();
            return sector == Sector.Zero ? map.Name : sector.Name;
        }

        protected override void OnModuleLoaded(EventArgs e) {
            UpdateFonts(FontSize.Value / 100f);

            GameService.Gw2Mumble.CurrentMap.MapChanged += OnMapChanged;
            GameService.Overlay.UserLocaleChanged       += OnUserLocaleChanged;

            VerticalPosition.SettingChanged += OnVerticalPositionChanged;
            FontSize.SettingChanged         += OnFontSizeChanged;

            Dissolve.SettingChanged        += PopNotification;
            Translate.SettingChanged       += PopNotification;
            UnderlineHeader.SettingChanged += PopNotification;
            OverlapHeader.SettingChanged   += PopNotification;
            MuteReveal.SettingChanged      += PopNotification;
            MuteVanish.SettingChanged      += PopNotification;

            _showSectorOnCompass.SettingChanged += OnShowSectorOnCompassChanged;

            OnMapChanged(GameService.Gw2Mumble, new ValueEventArgs<int>(GameService.Gw2Mumble.CurrentMap.Id));

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        /// <inheritdoc />
        protected override void Unload() {
            _unloading = true;

            _showSectorOnCompass.SettingChanged -= OnShowSectorOnCompassChanged;
            Compass?.Dispose();
            VanishSound?.Dispose();
            DecodeSound?.Dispose();
            DissolveEffect?.Dispose();
            KrytanFont?.Dispose();
            KrytanFontSmall?.Dispose();
            TitlingFont?.Dispose();
            TitlingFontSmall?.Dispose();
            VerticalPosition.SettingChanged -= OnVerticalPositionChanged;
            FontSize.SettingChanged         -= OnFontSizeChanged;
            Dissolve.SettingChanged         -= PopNotification;
            Translate.SettingChanged        -= PopNotification;
            UnderlineHeader.SettingChanged  -= PopNotification;
            OverlapHeader.SettingChanged    -= PopNotification;
            MuteReveal.SettingChanged       -= PopNotification;
            MuteVanish.SettingChanged       -= PopNotification;

            _notificationIndicator?.Dispose();

            GameService.Gw2Mumble.CurrentMap.MapChanged -= OnMapChanged;
            GameService.Overlay.UserLocaleChanged       -= OnUserLocaleChanged;

            // All static members must be manually unset
            Instance = null;
        }

        private void OnShowSectorOnCompassChanged(object sender, ValueChangedEventArgs<bool> e) {
            Compass?.Dispose();
            Compass = null;
            if (e.NewValue) {
                Compass = new CompassService();
                Compass.Show(_lastSectorName);
            }
        }

        private void OnFontSizeChanged(object sender, ValueChangedEventArgs<float> e) {
            UpdateFonts(e.NewValue / 100f);
            ShowPreviewSettingIndicator();
        }

        private void OnVerticalPositionChanged(object sender, ValueChangedEventArgs<float> e) {
            ShowPreviewSettingIndicator();
        }

        private void ShowPreviewSettingIndicator() {
            _lastIndicatorChange = DateTime.UtcNow;

            _notificationIndicator ??= new NotificationIndicator(_currentMap.Name, _currentSector.Name) {
                Parent = GameService.Graphics.SpriteScreen
            };
        }

        private void PopNotification(object sender, ValueChangedEventArgs<bool> e) {
            ShowNotification(_includeMapInSectorNotification.Value ? _currentMap.Name : null, _currentSector.Name);
        }

        private void ShowNotification(string header, string text) {
            MapNotification.ShowNotification(header, text,
                                            _showDuration.Value / 100f * 5f,
                                            _fadeInDuration.Value / 100f * 3f,
                                             _fadeOutDuration.Value / 100f * 3f,
                                             _effectDuration.Value / 100f * 3f);
        }

        private void UpdateFonts(float fontSize = 0.92f) {
            var size = (int)Math.Round((fontSize + 0.35f) * 37);

            KrytanFont?.Dispose();
            KrytanFontSmall?.Dispose();
            KrytanFont      = ContentsManager.GetBitmapFont("fonts/NewKrytan.ttf", size + 10);
            KrytanFontSmall = ContentsManager.GetBitmapFont("fonts/NewKrytan.ttf", size - 2);

            TitlingFont?.Dispose();
            TitlingFontSmall?.Dispose();
            TitlingFont      = ContentsManager.GetBitmapFont("fonts/StoweTitling.ttf", size);
            TitlingFontSmall = ContentsManager.GetBitmapFont("fonts/StoweTitling.ttf", size - (int)(MathHelper.Clamp(fontSize, 0.2f, 1) * 12));
        }

        private void OnUserLocaleChanged(object o, ValueEventArgs<System.Globalization.CultureInfo> e) {
            _mapRepository    = new AsyncCache<int, Map>(RequestMap);
            _sectorRepository = new AsyncCache<int, List<Sector>>(RequestSectors);
            _currentMap       = new Map();
            _currentSector    = Sector.Zero;
            _previousSector   = Sector.Zero;
        }

        private async void OnMapChanged(object o, ValueEventArgs<int> e) {
            _lastUpdate = DateTime.UtcNow;

            var map = await _mapRepository.GetItem(e.Value);

            _currentMap = map;

            if (!_toggleMapNotification.Value) {
                return;
            }

            var mapName = await GetTrueMapName(map);

            _lastSectorName = MapNotification.FilterDisplayName(mapName);
            if (_showSectorOnCompass.Value) {
                Compass?.Show(_lastSectorName);
            }

            ShowNotification(_includeRegionInMapNotification.Value ? map.RegionName : null, mapName);
        }

        private async Task<Sector> GetSector(Map map) {
            var playerLocation = GameService.Gw2Mumble.RawClient.AvatarPosition.ToContinentCoords(CoordsUnit.MUMBLE, map.MapRect, map.ContinentRect).SwapYz().ToPlane();
            var sectors = await _sectorRepository.GetItem(GameService.Gw2Mumble.CurrentMap.Id);
            var sector = sectors?.FirstOrDefault(sector => sector.Contains(playerLocation.X, playerLocation.Y));
            return sector ?? Sector.Zero;
        }

        private async Task<List<Sector>> RequestSectors(int mapId) {
            var map = await _mapRepository.GetItem(mapId);

            var geometryZone = new List<Sector>();

            foreach (var floor in map.Floors) {
                var sectors = await TaskUtil.RetryAsync(() => Gw2ApiManager.Gw2ApiClient.V2.Continents[map.ContinentId].Floors[floor].Regions[map.RegionId].Maps[map.Id].Sectors.AllAsync());
                if (sectors != null && sectors.Any()) {
                    geometryZone.AddRange(sectors.DistinctBy(sector => sector.Id).Select(sector => new Sector(sector)));
                }
            }
            return geometryZone;
        }

        private async Task<Map> RequestMap(int id) {
            var map = await TaskUtil.RetryAsync(() => Gw2ApiManager.Gw2ApiClient.V2.Maps.GetAsync(id));
            return map ?? new Map();
        }
    }
}
