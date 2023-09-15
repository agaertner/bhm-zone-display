using Blish_HUD;
using Blish_HUD.Extended;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using Nekres.Regions_Of_Tyria.Geometry;
using Nekres.Regions_Of_Tyria.UI.Controls;
using RBush;
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

        /// <summary>
        /// Fires when the in-game sector changes.
        /// </summary>
        public static event EventHandler<ValueEventArgs<int>> SectorChanged;

        private static int _prevSectorId;
        public static int CurrentSector {
            get => _prevSectorId;
            private set {
                if (value == _prevSectorId) {
                    return;
                }

                _prevSectorId = value;
                SectorChanged?.Invoke(Instance, new ValueEventArgs<int>(value));
            }
        }

        private SettingEntry<float> _showDurationSetting;
        private SettingEntry<float> _fadeInDurationSetting;
        private SettingEntry<float> _fadeOutDurationSetting;
        private SettingEntry<float> _effectDurationSetting;
        private SettingEntry<bool>  _toggleMapNotificationSetting;
        private SettingEntry<bool>  _toggleSectorNotificationSetting;
        private SettingEntry<bool>  _includeRegionInMapNotificationSetting;
        private SettingEntry<bool>  _includeMapInSectorNotification;
        private SettingEntry<bool>  _hideInCombatSetting;

        internal SettingEntry<bool>                         TranslateSetting;
        internal SettingEntry<MapNotification.RevealEffect> RevealEffectSetting;
        internal SettingEntry<float>                        VerticalPositionSetting;


        private DateTime              _lastIndicatorChange = DateTime.UtcNow;
        private NotificationIndicator _notificationIndicator;

        private float _showDuration;
        private float _fadeInDuration;
        private float _fadeOutDuration;
        private float _effectDuration;

        private AsyncCache<int, Map>           _mapRepository;
        private AsyncCache<int, RBush<Sector>> _sectorRepository;

        private int      _prevMapId;
        private double   _lastRun;
        private DateTime _lastUpdate;

        [ImportingConstructor]
        public RegionsOfTyria([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) {
            Instance = this;
        }

        protected override void DefineSettings(SettingCollection settings) {
            var generalCol = settings.AddSubCollection("general", true, () => "General");

            TranslateSetting = generalCol.DefineSetting("translate", true,
                                                        () => "Translate from New Krytan",
                                                        () => "Makes zone names appear in New Krytan before they are revealed to you.");

            RevealEffectSetting = generalCol.DefineSetting("reveal_effect", MapNotification.RevealEffect.Decode,
                                                           () => "Reveal Effect",
                                                           () => "The type of transition to use for revealing zone names.");

            VerticalPositionSetting = generalCol.DefineSetting("pos_y", 25f,
                                                               () => "Vertical Position",
                                                               () => "Sets the vertical position of area notifications.");

            _hideInCombatSetting = generalCol.DefineSetting("hide_zones_in_combat", true, 
                                                              () => "Disable Zone Notifications in Combat", 
                                                              () => "Disables zone notifications during combat.");

            var durationCol = settings.AddSubCollection("durations", true, () => "Durations");

            _showDurationSetting = durationCol.DefineSetting("show", 80f,
                                                             () => "Show Duration",
                                                             () => "The duration in which to stay in full opacity.");

            _fadeInDurationSetting = durationCol.DefineSetting("fade_in", 45f,
                                                               () => "Fade-In Duration",
                                                               () => "The duration of the fade-in.");

            _fadeOutDurationSetting = durationCol.DefineSetting("fade_out", 65f,
                                                                () => "Fade-Out Duration",
                                                                () => "The duration of the fade-out.");

            _effectDurationSetting = durationCol.DefineSetting("effect", 30f,
                                                               () => "Reveal Effect Duration",
                                                               () => "The duration of the reveal or translation effect.");

            var mapCol = settings.AddSubCollection("map_alert", true, () => "Map Notification");

            _toggleMapNotificationSetting = mapCol.DefineSetting("enabled", true,
                                                                    () => "Enabled",
                                                                    () => "Shows a map's name after entering it.");

            _includeRegionInMapNotificationSetting = mapCol.DefineSetting("prefix_region", true,
                                                                          () => "Include Region",
                                                                          () => "Shows the region's name above the map's name.");

            var sectorCol = settings.AddSubCollection("sector_alert", true, () => "Sector Notification");

            _toggleSectorNotificationSetting = sectorCol.DefineSetting("enabled", true,
                                                                       () => "Enabled",
                                                                       () => "Shows a sector's name after entering.");
            _includeMapInSectorNotification = sectorCol.DefineSetting("prefix_map", true,
                                                                      () => "Include Map",
                                                                      () => "Shows the map's name above the sector's name.");
        }

        protected override void Initialize() {
            _mapRepository    = new AsyncCache<int, Map>(RequestMap);
            _sectorRepository = new AsyncCache<int, RBush<Sector>>(RequestSectors);
        }

        protected override async void Update(GameTime gameTime) {
            if (DateTime.UtcNow.Subtract(_lastIndicatorChange).TotalMilliseconds > 250) {
                _notificationIndicator?.Dispose();
                _notificationIndicator = null;
            }

            if (!GameService.Gw2Mumble.IsAvailable || !GameService.GameIntegration.Gw2Instance.IsInGame) {
                return;
            }

            if (!_toggleSectorNotificationSetting.Value) {
                return;
            }

            if (_hideInCombatSetting.Value && GameService.Gw2Mumble.PlayerCharacter.IsInCombat) {
                return;
            }

            if (gameTime.TotalGameTime.TotalMilliseconds - _lastRun     < 10   || 
                DateTime.UtcNow.Subtract(_lastUpdate).TotalMilliseconds < 1000) {
                return;
            }

            _lastRun    = gameTime.ElapsedGameTime.TotalMilliseconds;
            _lastUpdate = DateTime.UtcNow;

            var currentMap    = await _mapRepository.GetItem(GameService.Gw2Mumble.CurrentMap.Id);
            var currentSector = await GetSector(currentMap);

            if (currentSector != null) {
                MapNotification.ShowNotification(_includeMapInSectorNotification.Value ? currentMap.Name : null, 
                                                 currentSector.Name, null, 
                                                 _showDuration, 
                                                 _fadeInDuration, 
                                                 _fadeOutDuration, 
                                                 _effectDuration);
            }
        }

        protected override void OnModuleLoaded(EventArgs e) {
            GameService.Gw2Mumble.CurrentMap.MapChanged += OnMapChanged;
            GameService.Overlay.UserLocaleChanged       += OnUserLocaleChanged;

            OnShowDurationSettingChanged(_showDurationSetting, new ValueChangedEventArgs<float>(0,       _showDurationSetting.Value));
            OnFadeInDurationSettingChanged(_fadeInDurationSetting, new ValueChangedEventArgs<float>(0,   _fadeInDurationSetting.Value));
            OnFadeOutDurationSettingChanged(_fadeOutDurationSetting, new ValueChangedEventArgs<float>(0, _fadeOutDurationSetting.Value));
            OnEffectDurationSettingChanged(_effectDurationSetting, new ValueChangedEventArgs<float>(0,   _effectDurationSetting.Value));

            _showDurationSetting.SettingChanged    += OnShowDurationSettingChanged;
            _fadeInDurationSetting.SettingChanged  += OnFadeInDurationSettingChanged;
            _fadeOutDurationSetting.SettingChanged += OnFadeOutDurationSettingChanged;
            _effectDurationSetting.SettingChanged  += OnEffectDurationSettingChanged;

            VerticalPositionSetting.SettingChanged += OnVerticalPositionSettingChanged;

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        private void OnShowDurationSettingChanged(object    o, ValueChangedEventArgs<float> e) => _showDuration = MathHelper.Clamp(e.NewValue,    0, 100) / 100f * 5.0f;
        private void OnFadeInDurationSettingChanged(object  o, ValueChangedEventArgs<float> e) => _fadeInDuration = MathHelper.Clamp(e.NewValue,  0, 100) / 100f * 3.0f;
        private void OnFadeOutDurationSettingChanged(object o, ValueChangedEventArgs<float> e) => _fadeOutDuration = MathHelper.Clamp(e.NewValue, 0, 100) / 100f * 3.0f;
        private void OnEffectDurationSettingChanged(object  o, ValueChangedEventArgs<float> e) => _effectDuration = MathHelper.Clamp(e.NewValue, 0, 100) / 100f * 3.0f;

        /// <inheritdoc />
        protected override void Unload() {
            _notificationIndicator?.Dispose();

            VerticalPositionSetting.SettingChanged -= OnVerticalPositionSettingChanged;

            _showDurationSetting.SettingChanged         -= OnShowDurationSettingChanged;
            _fadeInDurationSetting.SettingChanged       -= OnFadeInDurationSettingChanged;
            _fadeOutDurationSetting.SettingChanged      -= OnFadeOutDurationSettingChanged;
            _effectDurationSetting.SettingChanged       -= OnEffectDurationSettingChanged;

            GameService.Gw2Mumble.CurrentMap.MapChanged -= OnMapChanged;
            GameService.Overlay.UserLocaleChanged       -= OnUserLocaleChanged;

            // All static members must be manually unset
            Instance = null;
        }

        private void OnVerticalPositionSettingChanged(object o, ValueChangedEventArgs<float> e) {
            _lastIndicatorChange = DateTime.UtcNow;

            _notificationIndicator ??= new NotificationIndicator {
                Parent = GameService.Graphics.SpriteScreen
            };
        }

        private void OnUserLocaleChanged(object o, ValueEventArgs<System.Globalization.CultureInfo> e) {
            _mapRepository    = new AsyncCache<int, Map>(RequestMap);
            _sectorRepository = new AsyncCache<int, RBush<Sector>>(RequestSectors);
        }

        private async void OnMapChanged(object o, ValueEventArgs<int> e) {
            if (!_toggleMapNotificationSetting.Value) {
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

            MapNotification.ShowNotification(_includeRegionInMapNotificationSetting.Value ? header : null, 
                                             mapName, null, 
                                             _showDuration, 
                                             _fadeInDuration, 
                                             _fadeOutDuration, 
                                             _effectDuration);
        }

        private async Task<Sector> GetSector(Map currentMap) {
            if (currentMap == null) {
                return null;
            }

            var playerLocation = GameService.Gw2Mumble.RawClient.AvatarPosition.ToContinentCoords(CoordsUnit.MUMBLE, currentMap.MapRect, currentMap.ContinentRect).SwapYz().ToPlane();
            var rtree = await _sectorRepository.GetItem(GameService.Gw2Mumble.CurrentMap.Id);

            if (rtree == null) {
                return null;
            }

            var foundPoints    = rtree.Search(new Envelope(playerLocation.X, playerLocation.Y, playerLocation.X, playerLocation.Y));
            
            if (foundPoints.Count == 0 || _prevSectorId.Equals(foundPoints[0].Id)) {
                return null;
            }

            CurrentSector = foundPoints[0].Id;
            return foundPoints[0];
        }

        private async Task<RBush<Sector>> RequestSectors(int mapId) {

            var map = await _mapRepository.GetItem(mapId);

            if (map == null) {
                return null;
            }

            IEnumerable<Sector> geometryZone = new HashSet<Sector>();

            var comparer = ProjectionEqualityComparer<Sector>.Create(x => x.Id);

            foreach (var floor in map.Floors) {
                var sectors = await TaskUtil.RetryAsync(() => Gw2ApiManager.Gw2ApiClient.V2.Continents[map.ContinentId].Floors[floor].Regions[map.RegionId].Maps[map.Id].Sectors.AllAsync());
                if (sectors == null || !sectors.Any()) {
                    continue;
                }
                geometryZone = geometryZone.Union(sectors.Select(x => new Sector(x)), comparer);
            }

            var rtree = new RBush<Sector>();
            foreach (var sector in geometryZone) {
                rtree.Insert(sector);
            }
            //rtree.BulkLoad(geometryZone); // Unfortunately, this results in loss of precision in zone detection.
            return rtree;
        }

        private async Task<Map> RequestMap(int id) {
            return await TaskUtil.RetryAsync(() => Gw2ApiManager.Gw2ApiClient.V2.Maps.GetAsync(id));
        }
    }
}
