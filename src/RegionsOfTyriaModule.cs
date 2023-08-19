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
    public class RegionsOfTyriaModule : Module {

        internal static readonly Logger Logger = Logger.GetLogger(typeof(RegionsOfTyriaModule));

        internal static RegionsOfTyriaModule ModuleInstance;

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

        private SettingEntry<float> _showDurationSetting;
        private SettingEntry<float> _fadeInDurationSetting;
        private SettingEntry<float> _fadeOutDurationSetting;
        private SettingEntry<bool>  _toggleMapNotificationSetting;
        private SettingEntry<bool>  _toggleSectorNotificationSetting;
        private SettingEntry<bool>  _includeRegionInMapNotificationSetting;
        private SettingEntry<bool>  _includeMapInSectorNotification;

        internal SettingEntry<float> VerticalPositionSetting;

        private DateTime                 _lastVerticalChange = DateTime.UtcNow;
        private ControlPositionIndicator _verticalIndicator;

        private float _showDuration;
        private float _fadeInDuration;
        private float _fadeOutDuration;

        private AsyncCache<int, Map>           _mapRepository;
        private AsyncCache<int, RBush<Sector>> _sectorRepository;

        private static int _prevSectorId;
        public static int CurrentSector {
            get => _prevSectorId;
            private set {
                if (value == _prevSectorId) {
                    return;
                }

                _prevSectorId = value;
                SectorChanged?.Invoke(ModuleInstance, new ValueEventArgs<int>(value));
            }
        }

        private int      _prevMapId;
        private double   _lastRun;
        private DateTime _lastUpdate;

        [ImportingConstructor]
        public RegionsOfTyriaModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) {
            ModuleInstance = this;
        }

        protected override void DefineSettings(SettingCollection settings) {
            var toggleCol = settings.AddSubCollection("notifications", true, () => "Notifications");

            _toggleMapNotificationSetting = toggleCol.DefineSetting("enable_map_change", true,
                                                                    () => "Notify Map Change",
                                                                    () => "Shows a map's name after entering it.");

            _includeRegionInMapNotificationSetting = toggleCol.DefineSetting("region_in_subtitle", true,
                                                                             () => "Display Region",
                                                                             () => "Shows the region below the map notification.");

            _toggleSectorNotificationSetting = toggleCol.DefineSetting("enable_sector_change", true,
                                                                       () => "Notify Sector Change",
                                                                       () => "Shows a sector's name after entering.");

            _includeMapInSectorNotification = toggleCol.DefineSetting("map_in_subtitle", true,
                                                                      () => "Display Map",
                                                                      () => "Shows the map name below the sector notification.");

            var durationCol = settings.AddSubCollection("durations", true, () => "Durations");

            _showDurationSetting = durationCol.DefineSetting("show", 40f,
                                                             () => "Show Duration",
                                                             () => "The duration in which to stay in full opacity.");

            _fadeInDurationSetting = durationCol.DefineSetting("fade_in", 20f,
                                                               () => "Fade-In Duration",
                                                               () => "The duration of the fade-in.");

            _fadeOutDurationSetting = durationCol.DefineSetting("fade_out", 20f,
                                                                () => "Fade-Out Duration",
                                                                () => "The duration of the fade-out.");

            var positionCol = settings.AddSubCollection("position", true, () => "Position");

            VerticalPositionSetting = positionCol.DefineSetting("pos_y", 30f,
                                                                () => "Vertical Position",
                                                                () => "Sets the vertical position of area notifications.");
        }

        protected override void Initialize() {
            _mapRepository    = new AsyncCache<int, Map>(RequestMap);
            _sectorRepository = new AsyncCache<int, RBush<Sector>>(RequestSectors);
        }

        protected override async void Update(GameTime gameTime) {

            if (gameTime.TotalGameTime.TotalMilliseconds - _lastRun < 10 || DateTime.UtcNow.Subtract(_lastUpdate).TotalMilliseconds < 1000 || !_toggleSectorNotificationSetting.Value || !GameService.Gw2Mumble.IsAvailable
             || !GameService.GameIntegration.Gw2Instance.IsInGame) {
                return;
            }

            _lastRun    = gameTime.ElapsedGameTime.TotalMilliseconds;
            _lastUpdate = DateTime.UtcNow;

            if (DateTime.UtcNow.Subtract(_lastVerticalChange).TotalMilliseconds > 250) {
                _verticalIndicator?.Dispose();
                _verticalIndicator = null;
            }

            var currentMap    = await _mapRepository.GetItem(GameService.Gw2Mumble.CurrentMap.Id);
            var currentSector = await GetSector(currentMap);

            if (currentSector != null) {
                MapNotification.ShowNotification(currentSector.Name, _includeMapInSectorNotification.Value ? currentMap.Name : null, null, _showDuration, _fadeInDuration, _fadeOutDuration);
            }
        }

        protected override void OnModuleLoaded(EventArgs e) {
            GameService.Gw2Mumble.CurrentMap.MapChanged += OnMapChanged;
            GameService.Overlay.UserLocaleChanged       += OnUserLocaleChanged;

            OnShowDurationSettingChanged(_showDurationSetting, new ValueChangedEventArgs<float>(0,       _showDurationSetting.Value));
            OnFadeInDurationSettingChanged(_fadeInDurationSetting, new ValueChangedEventArgs<float>(0,   _fadeInDurationSetting.Value));
            OnFadeOutDurationSettingChanged(_fadeOutDurationSetting, new ValueChangedEventArgs<float>(0, _fadeOutDurationSetting.Value));

            _showDurationSetting.SettingChanged    += OnShowDurationSettingChanged;
            _fadeInDurationSetting.SettingChanged  += OnFadeInDurationSettingChanged;
            _fadeOutDurationSetting.SettingChanged += OnFadeOutDurationSettingChanged;
            VerticalPositionSetting.SettingChanged += OnVerticalPositionSettingChanged;

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        private void OnShowDurationSettingChanged(object    o, ValueChangedEventArgs<float> e) => _showDuration = MathHelper.Clamp(e.NewValue,    0, 100) / 10;
        private void OnFadeInDurationSettingChanged(object  o, ValueChangedEventArgs<float> e) => _fadeInDuration = MathHelper.Clamp(e.NewValue,  0, 100) / 10;
        private void OnFadeOutDurationSettingChanged(object o, ValueChangedEventArgs<float> e) => _fadeOutDuration = MathHelper.Clamp(e.NewValue, 0, 100) / 10;

        /// <inheritdoc />
        protected override void Unload() {
            _verticalIndicator?.Dispose();

            VerticalPositionSetting.SettingChanged -= OnVerticalPositionSettingChanged;

            _showDurationSetting.SettingChanged         -= OnShowDurationSettingChanged;
            _fadeInDurationSetting.SettingChanged       -= OnFadeInDurationSettingChanged;
            _fadeOutDurationSetting.SettingChanged      -= OnFadeOutDurationSettingChanged;
            GameService.Gw2Mumble.CurrentMap.MapChanged -= OnMapChanged;
            GameService.Overlay.UserLocaleChanged       -= OnUserLocaleChanged;

            // All static members must be manually unset
            ModuleInstance = null;
        }

        private void OnVerticalPositionSettingChanged(object o, ValueChangedEventArgs<float> e) {
            _lastVerticalChange = DateTime.UtcNow;

            _verticalIndicator ??= new ControlPositionIndicator {
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

            MapNotification.ShowNotification(mapName, _includeRegionInMapNotificationSetting.Value ? header : null, null, _showDuration, _fadeInDuration, _fadeOutDuration);
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
