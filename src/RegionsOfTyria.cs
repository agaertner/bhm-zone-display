using Blish_HUD;
using Blish_HUD.Extended;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
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
        private SettingEntry<bool>  _hideInCombat;

        internal SettingEntry<bool>                         Translate;
        internal SettingEntry<MapNotification.RevealEffect> RevealEffect;
        internal SettingEntry<float>                        VerticalPosition;
        internal SettingEntry<float>                        FontSize;

        private AsyncCache<int, Map>          _mapRepository;
        private AsyncCache<int, List<Sector>> _sectorRepository;

        private string _currentMap;
        private string _currentSector;
        private int    _prevSectorId;
        private int    _prevMapId;

        private DateTime              _lastIndicatorChange = DateTime.UtcNow;
        private NotificationIndicator _notificationIndicator;

        private double   _lastRun;
        private DateTime _lastUpdate = DateTime.UtcNow;

        [ImportingConstructor]
        public RegionsOfTyria([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) {
            Instance = this;
        }

        protected override void DefineSettings(SettingCollection settings) {
            var generalCol = settings.AddSubCollection("general", true, () => "General");

            Translate = generalCol.DefineSetting("translate", true,
                                                        () => "Translate from New Krytan",
                                                        () => "Makes zone notifications appear in New Krytan before they are revealed to you.");

            RevealEffect = generalCol.DefineSetting("reveal_effect", MapNotification.RevealEffect.Decode,
                                                           () => "Reveal Effect",
                                                           () => "The type of transition to use for revealing zone names.");

            VerticalPosition = generalCol.DefineSetting("pos_y", 25f,
                                                               () => "Vertical Position",
                                                               () => "Sets the vertical position of area notifications.");

            FontSize = generalCol.DefineSetting("font_size", 76f, 
                                                        () => "Font Size", 
                                                        () => "Sets the size of the zone notification text.");

            _hideInCombat = generalCol.DefineSetting("hide_if_combat", true, 
                                                              () => "Disable Zone Notifications in Combat", 
                                                              () => "Disables zone notifications during combat.");

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
        }

        protected override void Initialize() {
            _mapRepository    = new AsyncCache<int, Map>(RequestMap);
            _sectorRepository = new AsyncCache<int, List<Sector>>(RequestSectors);
        }

        protected override async void Update(GameTime gameTime) {
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
            if (playerSpeed > 55) {
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

            var currentMap    = await _mapRepository.GetItem(GameService.Gw2Mumble.CurrentMap.Id);
            var currentSector = await GetSector(currentMap);

            if (currentSector != null) {
                _currentMap = currentMap.Name;
                _currentSector = currentSector.Name;

                MapNotification.ShowNotification(_includeMapInSectorNotification.Value ? currentMap.Name : null, 
                                                 currentSector.Name, null,
                                                 _showDuration.Value / 100f * 5f,
                                                 _fadeInDuration.Value / 100f * 3f,
                                                 _fadeOutDuration.Value / 100f * 3f,
                                                 _effectDuration.Value / 100f * 3f);
            }
        }

        protected override void OnModuleLoaded(EventArgs e) {
            MapNotification.UpdateFonts(FontSize.Value / 100f);

            GameService.Gw2Mumble.CurrentMap.MapChanged += OnMapChanged;
            GameService.Overlay.UserLocaleChanged       += OnUserLocaleChanged;

            VerticalPosition.SettingChanged += OnVerticalPositionChanged;
            FontSize.SettingChanged         += OnFontSizeChanged;

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        /// <inheritdoc />
        protected override void Unload() {
            _notificationIndicator?.Dispose();

            VerticalPosition.SettingChanged -= OnVerticalPositionChanged;
            FontSize.SettingChanged         -= OnFontSizeChanged;

            GameService.Gw2Mumble.CurrentMap.MapChanged -= OnMapChanged;
            GameService.Overlay.UserLocaleChanged       -= OnUserLocaleChanged;

            // All static members must be manually unset
            Instance = null;
        }

        private void OnFontSizeChanged(object sender, ValueChangedEventArgs<float> e) {
            MapNotification.UpdateFonts(e.NewValue / 100f);
            ShowPreviewSettingIndicator();
        }

        private void OnVerticalPositionChanged(object sender, ValueChangedEventArgs<float> e) {
            ShowPreviewSettingIndicator();
        }

        private void ShowPreviewSettingIndicator() {
            _lastIndicatorChange = DateTime.UtcNow;

            _notificationIndicator ??= new NotificationIndicator(_currentMap, _currentSector) {
                Parent = GameService.Graphics.SpriteScreen
            };
        }

        private void OnUserLocaleChanged(object o, ValueEventArgs<System.Globalization.CultureInfo> e) {
            _mapRepository    = new AsyncCache<int, Map>(RequestMap);
            _sectorRepository = new AsyncCache<int, List<Sector>>(RequestSectors);
        }

        private async void OnMapChanged(object o, ValueEventArgs<int> e) {
            if (!_toggleMapNotification.Value) {
                return;
            }

            var currentMap = await _mapRepository.GetItem(e.Value);

            if (currentMap == null || currentMap.Id == _prevMapId) {
                return;
            }

            _prevMapId = currentMap.Id;

            var header = currentMap.RegionName;
            var mapName   = currentMap.Name;

            // Some maps consist of just a single sector and hide their actual name in it.
            if (mapName.Equals(header, StringComparison.InvariantCultureIgnoreCase)) {
                var currentSector = await GetSector(currentMap);

                if (currentSector != null && !string.IsNullOrEmpty(currentSector.Name)) {
                    mapName = currentSector.Name;
                }
            }

            MapNotification.ShowNotification(_includeRegionInMapNotification.Value ? header : null, 
                                             mapName, null, 
                                             _showDuration.Value / 100f * 5f,
                                             _fadeInDuration.Value / 100f * 3f,
                                             _fadeOutDuration.Value / 100f * 3f,
                                             _effectDuration.Value / 100f * 3f);
        }

        private async Task<Sector> GetSector(Map currentMap) {
            if (currentMap == null) {
                return null;
            }

            var playerLocation = GameService.Gw2Mumble.RawClient.AvatarPosition.ToContinentCoords(CoordsUnit.MUMBLE, currentMap.MapRect, currentMap.ContinentRect).SwapYz().ToPlane();
            var sectors = await _sectorRepository.GetItem(GameService.Gw2Mumble.CurrentMap.Id);

            var sector = sectors?.FirstOrDefault(sector => sector.Contains(playerLocation.X, playerLocation.Y));

            if (sector == null || _prevSectorId == sector.Id) {
                return null;
            }

            _currentSector = sector.Name;
            _prevSectorId = sector.Id;
            return sector;
        }

        private async Task<List<Sector>> RequestSectors(int mapId) {

            var map = await _mapRepository.GetItem(mapId);

            if (map == null) {
                return null;
            }

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
            return await TaskUtil.RetryAsync(() => Gw2ApiManager.Gw2ApiClient.V2.Maps.GetAsync(id));
        }
    }
}
