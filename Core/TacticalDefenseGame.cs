using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using TacticalDefenseGame.Managers;
using TacticalDefenseGame.Models;
using TacticalDefenseGame.Entities;

namespace TacticalDefenseGame.Core
{
    public class TacticalDefenseGame : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        
        // Rendering Helpers
        private Texture2D _pixel;
        private RenderTarget2D _renderTarget;
        private int _virtualWidth;
        private int _virtualHeight;

        // Asset Storage
        private Dictionary<string, Texture2D> _textures = new();

        // HUD Constants
        private const int HudHeight = 100;

        // Managers
        private GridManager _gridManager;
        private EntityManager _entityManager;
        private ResourceManager _resourceManager;
        private WaveManager _waveManager;
        private TelemetryManager _telemetryManager;

        // Points of Interest
        private Point _spawnPoint = new Point(0, 7);
        private Point _corePoint = new Point(14, 7);

        // UI / Input State
        private Direction _currentFacing = Direction.Right;
        private KeyboardState _lastKeyboardState;
        private bool _showHelp = false;
        private long _lastClickTicks = 0;

        // Game State
        private GameState _currentState;

        public TacticalDefenseGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Internal Resolution (Grid + Large HUD)
            _virtualWidth = GridManager.GridSize * GridManager.CellSize;
            _virtualHeight = GridManager.GridSize * GridManager.CellSize + HudHeight;

            // Opening Window Size (Reduced to 75%)
            _graphics.PreferredBackBufferWidth = 720;
            _graphics.PreferredBackBufferHeight = 795;
            _graphics.ApplyChanges();

            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += (s, e) => {
                _graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
                _graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
                _graphics.ApplyChanges();
            };
        }

        protected override void Initialize()
        {
            _gridManager = new GridManager();
            var points = _gridManager.GenerateRandomPoints();
            _spawnPoint = points.spawn;
            _corePoint = points.core;

            _resourceManager = new ResourceManager(100f);
            _entityManager = new EntityManager();
            
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _waveManager = new WaveManager();
            _telemetryManager = new TelemetryManager();
            _currentState = GameState.Gameplay;

            base.Initialize();
        }

        private Texture2D LoadPngTexture(string fileName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] searchPaths = {
                System.IO.Path.Combine(baseDir, "Assets", fileName),
                System.IO.Path.Combine(baseDir, "..", "..", "..", "Assets", fileName),
                System.IO.Path.Combine(baseDir, "..", "..", "Assets", fileName),
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets", fileName),
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "Assets", fileName),
                System.IO.Path.Combine("Assets", fileName)
            };

            foreach (var path in searchPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    try {
                        using (var stream = System.IO.File.OpenRead(path))
                            return Texture2D.FromStream(GraphicsDevice, stream);
                    } catch { }
                }
            }

            // High-visibility fallback (Magenta) to confirm load failure
            Texture2D error = new Texture2D(GraphicsDevice, 2, 2);
            Color[] data = new Color[4];
            for (int i = 0; i < 4; i++) data[i] = Color.Magenta;
            error.SetData(data);
            return error;
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _renderTarget = new RenderTarget2D(GraphicsDevice, _virtualWidth, _virtualHeight);

            _textures["tower_base"] = LoadPngTexture("tower_base.png");
            _textures["tower_ice"] = LoadPngTexture("tower_ice.png");
            _textures["tower_laser"] = LoadPngTexture("tower_laser.png");
            _textures["enemy_standard"] = LoadPngTexture("enemy_standard.png");
            _textures["enemy_speedster"] = LoadPngTexture("enemy_speedster.png");
            _textures["enemy_tank"] = LoadPngTexture("enemy_tank.png");
            _textures["enemy_phaser"] = LoadPngTexture("enemy_phaser.png");
            _textures["enemy_support"] = LoadPngTexture("enemy_support.png");
            _textures["enemy_striker"] = LoadPngTexture("enemy_striker.png");
            _textures["enemy_harbinger"] = LoadPngTexture("enemy_harbinger.png");
            _textures["hazard_obstacle"] = LoadPngTexture("hazard_obstacle.png");
            _textures["hazard_volcanic"] = LoadPngTexture("hazard_volcanic.png");
            _textures["hazard_corrosive"] = LoadPngTexture("hazard_corrosive.png");
            _textures["enemy_spawn"] = LoadPngTexture("enemy_spawn.png");
            _textures["player_base_core"] = LoadPngTexture("player_base_core.png");
            _textures["building_power_plant"] = LoadPngTexture("building_power_plant.png");

            _entityManager.Initialize(_gridManager, _resourceManager, _textures);
            _gridManager.LoadTextures(_textures);
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboardState.IsKeyDown(Keys.Escape))
                Exit();

            if (_currentState == GameState.Gameplay)
            {
                if (keyboardState.IsKeyDown(Keys.R) && _lastKeyboardState.IsKeyUp(Keys.R))
                    _currentFacing = (Direction)(((int)_currentFacing + 1) % 4);

                var mouseState = Mouse.GetState();
                Rectangle vp = GetViewportRect();
                float scale = (float)vp.Width / _virtualWidth;
                int vMouseX = (int)((mouseState.X - vp.X) / scale);
                int vMouseY = (int)((mouseState.Y - vp.Y) / scale);

                if (mouseState.LeftButton == ButtonState.Pressed && vMouseX >= 900 && vMouseX <= 940 && vMouseY >= 10 && vMouseY <= 50)
                {
                    if (DateTime.Now.Ticks / 10000 - _lastClickTicks > 200) {
                        _showHelp = !_showHelp;
                        _lastClickTicks = DateTime.Now.Ticks / 10000;
                    }
                }

                if (_showHelp) {
                    if (keyboardState.IsKeyDown(Keys.Space) || keyboardState.IsKeyDown(Keys.Enter)) _showHelp = false;
                }
                else {
                    int gridX = vMouseX / GridManager.CellSize;
                    int gridY = (vMouseY - HudHeight) / GridManager.CellSize;

                    if (mouseState.LeftButton == ButtonState.Pressed && vMouseY >= HudHeight) {
                        if (_resourceManager.Energy >= 25f && _gridManager.CanPlaceNode(gridX, gridY, _spawnPoint, _corePoint, _entityManager.ActiveEnemies)) {
                            _resourceManager.TrySpendEnergy(25f);
                            _telemetryManager.RecordEnergySpent(25f);
                            var node = _entityManager.GetNodeFromPool(new Point(gridX, gridY), _currentFacing);
                            _gridManager.PlaceNode(gridX, gridY, node, _corePoint);
                        }
                    }
                    if (mouseState.RightButton == ButtonState.Pressed && vMouseY >= HudHeight) _gridManager.RemoveNode(gridX, gridY, _spawnPoint, _corePoint);

                    if (vMouseY >= HudHeight) {
                        var cell = _gridManager.GetCell(gridX, gridY);
                        if (cell != null && cell.OccupyingNode != null && cell.OccupyingNode.Rank >= 2 && cell.OccupyingNode.Specialization == NodeSpecialization.None) {
                            if (keyboardState.IsKeyDown(Keys.D1) && _lastKeyboardState.IsKeyUp(Keys.D1) && _resourceManager.TrySpendScrap(40f)) cell.OccupyingNode.Specialization = NodeSpecialization.Cryo;
                            else if (keyboardState.IsKeyDown(Keys.D2) && _lastKeyboardState.IsKeyUp(Keys.D2) && _resourceManager.TrySpendScrap(40f)) cell.OccupyingNode.Specialization = NodeSpecialization.ArmorPiercing;
                        }
                    }

                    if (keyboardState.IsKeyDown(Keys.Space) && _lastKeyboardState.IsKeyUp(Keys.Space) && !_waveManager.IsWaveActive())
                    {
                        if (_waveManager.CurrentWave > 0) _telemetryManager.EndWave(_resourceManager.CoreHealth);
                        _waveManager.StartNextWave();
                        _telemetryManager.StartWave(_waveManager.CurrentWave);
                    }

                    // Command Bar Inputs
                    if (keyboardState.IsKeyDown(Keys.D3) && _lastKeyboardState.IsKeyUp(Keys.D3)) _resourceManager.UseAbility(0);
                    if (keyboardState.IsKeyDown(Keys.D4) && _lastKeyboardState.IsKeyUp(Keys.D4)) { if (_resourceManager.UseAbility(1)) _entityManager.HealAllNodes(10f); }
                    if (keyboardState.IsKeyDown(Keys.D5) && _lastKeyboardState.IsKeyUp(Keys.D5)) _resourceManager.UseAbility(2);

                    // Individual Repair (Key F)
                    if (keyboardState.IsKeyDown(Keys.F) && _lastKeyboardState.IsKeyUp(Keys.F)) {
                        var cell = _gridManager.GetCell(gridX, gridY);
                        if (cell != null && cell.OccupyingNode != null && cell.OccupyingNode.Health < cell.OccupyingNode.MaxHealth) {
                            if (_resourceManager.TrySpendScrap(10f)) cell.OccupyingNode.Health = MathHelper.Min(cell.OccupyingNode.MaxHealth, cell.OccupyingNode.Health + 25f);
                        }
                    }

                    _gridManager.Update(gameTime);
                    _resourceManager.Update(gameTime);
                    int reached = _entityManager.Update(gameTime, _spawnPoint, _corePoint, out float dmg);
                    if (reached > 0) { _resourceManager.TakeDamage(dmg); _telemetryManager.RecordDamageTaken(dmg); }
                    _waveManager.Update(gameTime, _entityManager, _gridManager, _spawnPoint, _corePoint);
                }

                if (_resourceManager.CoreHealth <= 0) _currentState = GameState.GameOver;
                else if (_waveManager.AllWavesComplete) _currentState = GameState.Victory;
            }
            else if (keyboardState.IsKeyDown(Keys.Enter)) Initialize();

            _lastKeyboardState = keyboardState;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.SetRenderTarget(_renderTarget);
            GraphicsDevice.Clear(Color.Black);

            var mouseState = Mouse.GetState();
            Rectangle vp = GetViewportRect();
            float scale = (float)vp.Width / _virtualWidth;
            int vMouseX = (int)((mouseState.X - vp.X) / scale);
            int vMouseY = (int)((mouseState.Y - vp.Y) / scale);
            Point hovered = new Point(vMouseX / GridManager.CellSize, (vMouseY - HudHeight) / GridManager.CellSize);

            _spriteBatch.Begin();
            DrawHUD();
            _spriteBatch.Draw(_pixel, new Rectangle(900, 20, 40, 40), Color.Gray * 0.4f);
            DrawString("?", 912, 25, 3, Color.White);
            _spriteBatch.End();

            _spriteBatch.Begin(transformMatrix: Matrix.CreateTranslation(0, HudHeight, 0));
            _gridManager.Draw(_spriteBatch, _pixel, hovered, _entityManager.ActiveEnemies);
            
            // Draw POIs with Pulsing Border Highlights (Standardized 1.5x Image, Fixed 1.0x Border)
            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 5) * 0.5f + 0.5f;
            
            // 1. Grid cell coordinates (Exactly 48x48) - This is the "tactical boundary"
            Rectangle spawnGridRect = new Rectangle(_spawnPoint.X * GridManager.CellSize, _spawnPoint.Y * GridManager.CellSize, GridManager.CellSize, GridManager.CellSize);
            Rectangle coreGridRect = new Rectangle(_corePoint.X * GridManager.CellSize, _corePoint.Y * GridManager.CellSize, GridManager.CellSize, GridManager.CellSize);

            // 2. Asset center positions for scaled drawing
            Vector2 spawnCenter = new Vector2(spawnGridRect.X + GridManager.CellSize / 2, spawnGridRect.Y + GridManager.CellSize / 2);
            Vector2 coreCenter = new Vector2(coreGridRect.X + GridManager.CellSize / 2, coreGridRect.Y + GridManager.CellSize / 2);

            // 3. Draw Unified Scaled Images (1.25x)
            Texture2D spawnTex = _textures["enemy_spawn"];
            Texture2D coreTex = _textures["player_base_core"];
            float sScale = (GridManager.CellSize * 1.25f) / spawnTex.Width;
            float cScale = (GridManager.CellSize * 1.25f) / coreTex.Width;

            _spriteBatch.Draw(spawnTex, spawnCenter, null, Color.White, 0f, new Vector2(spawnTex.Width / 2f, spawnTex.Height / 2f), sScale, SpriteEffects.None, 0);
            _spriteBatch.Draw(coreTex, coreCenter, null, Color.White, 0f, new Vector2(coreTex.Width / 2f, coreTex.Height / 2f), cScale, SpriteEffects.None, 0);

            // 4. Draw Borders (Locked to Grid Cell)
            DrawBorder(_spriteBatch, _pixel, spawnGridRect, 1, Color.Red * (0.3f + pulse * 0.4f));
            DrawBorder(_spriteBatch, _pixel, coreGridRect, 1, Color.DodgerBlue * (0.3f + pulse * 0.4f));

            _entityManager.Draw(_spriteBatch, _pixel);

            if (!_showHelp && _currentState == GameState.Gameplay && vMouseY >= HudHeight) {
                int pX = vMouseX / GridManager.CellSize, pY = (vMouseY - HudHeight) / GridManager.CellSize;
                if (pX >= 0 && pX < GridManager.GridSize && pY >= 0 && pY < GridManager.GridSize) {
                    bool can = _gridManager.CanPlaceNode(pX, pY, _spawnPoint, _corePoint, _entityManager.ActiveEnemies);
                    Texture2D ghostTex = _textures["tower_base"];
                    Vector2 origin = new Vector2(ghostTex.Width/2f, ghostTex.Height/2f);
                    Vector2 center = new Vector2(pX * GridManager.CellSize + GridManager.CellSize/2, pY * GridManager.CellSize + GridManager.CellSize/2);
                    float gRot = _currentFacing switch { Direction.Up => 0, Direction.Right => MathHelper.PiOver2, Direction.Down => MathHelper.Pi, Direction.Left => -MathHelper.PiOver2, _ => 0 };
                    float gScale = (GridManager.CellSize * 1.25f) / ghostTex.Width;
                    
                    // Show Tactical Attack Grid Preview
                    List<Point> pattern = new();
                    pattern.Add(new Point(pX, pY));
                    Point fv = _currentFacing switch { Direction.Up => new Point(0, -1), Direction.Down => new Point(0, 1), Direction.Left => new Point(-1, 0), Direction.Right => new Point(1, 0), _ => new Point(0, 0) };
                    Point sv = (_currentFacing == Direction.Up || _currentFacing == Direction.Down) ? new Point(1, 0) : new Point(0, 1);
                    
                    // Adjacency Check for Ghost
                    bool isAdj = false;
                    Point[] adjDirs = { new Point(0, 1), new Point(0, -1), new Point(1, 0), new Point(-1, 0) };
                    foreach(var d in adjDirs) {
                        var n = _gridManager.GetCell(pX + d.X, pY + d.Y);
                        if (n != null && n.OccupyingNode != null) { isAdj = true; break; }
                    }

                    for (int i = 0; i <= 1; i++) {
                        Point basePt = new Point(pX + fv.X * i, pY + fv.Y * i);
                        pattern.Add(basePt);
                        pattern.Add(new Point(basePt.X + sv.X, basePt.Y + sv.Y));
                        pattern.Add(new Point(basePt.X - sv.X, basePt.Y - sv.Y));
                    }
                    Point extPt = new Point(pX + fv.X * 2, pY + fv.Y * 2);
                    if (isAdj) { 
                        pattern.Add(extPt); 
                        pattern.Add(new Point(extPt.X + sv.X, extPt.Y + sv.Y)); 
                        pattern.Add(new Point(extPt.X - sv.X, extPt.Y - sv.Y)); 
                    } else {
                        pattern.Add(extPt);
                    }

                    foreach(var pt in pattern) if (pt.X >= 0 && pt.X < GridManager.GridSize && pt.Y >= 0 && pt.Y < GridManager.GridSize)
                        _spriteBatch.Draw(_pixel, new Rectangle(pt.X * GridManager.CellSize, pt.Y * GridManager.CellSize, GridManager.CellSize, GridManager.CellSize), Color.Cyan * 0.15f);

                    _spriteBatch.Draw(ghostTex, center, null, (can ? Color.White : Color.Red) * 0.4f, gRot, origin, gScale, SpriteEffects.None, 0);
                }
            }

            if (_currentState == GameState.GameOver) _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _virtualWidth, _virtualHeight - HudHeight), Color.Red * 0.5f);
            else if (_currentState == GameState.Victory) _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _virtualWidth, _virtualHeight - HudHeight), Color.Gold * 0.5f);
            _spriteBatch.End();

            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Viewport = new Viewport(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin();
            _spriteBatch.Draw(_renderTarget, vp, Color.White);
            _spriteBatch.End();

            if (_showHelp) { _spriteBatch.Begin(); DrawHelpOverlay(); _spriteBatch.End(); }
            base.Draw(gameTime);
        }

        private void DrawHUD()
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _virtualWidth, HudHeight), Color.DarkSlateGray);
            _spriteBatch.Draw(_pixel, new Rectangle(0, HudHeight - 2, _virtualWidth, 2), Color.Black * 0.5f);

            // 1. Stats
            DrawLabeledBar("CORE HP", 20, 10, 180, _resourceManager.CoreHealth / 100f, Color.Crimson);
            DrawNumber((int)_resourceManager.CoreHealth, 150, 12, 2, Color.White);

            DrawLabeledBar("ENERGY", 210, 10, 180, _resourceManager.Energy / 200f, Color.Cyan); 
            DrawNumber((int)_resourceManager.Energy, 340, 12, 2, Color.White);

            DrawLabeledBar("SCRAP", 400, 10, 120, _resourceManager.Scrap / 300f, Color.Gold);
            DrawNumber((int)_resourceManager.Scrap, 480, 12, 2, Color.White);

            // 2. Command Bar (Center)
            int bx = 550, by = 15;
            string[] names = { "OVERCLOCK", "REPAIR-GRID", "SHIELD" };
            Color[] colors = { Color.Orange, Color.LimeGreen, Color.DodgerBlue };
            for(int i=0; i<3; i++) {
                Rectangle r = new Rectangle(bx + i * 110, by, 100, 50);
                _spriteBatch.Draw(_pixel, r, Color.Black * 0.5f);
                DrawBorder(_spriteBatch, _pixel, r, 1, colors[i] * 0.5f);
                DrawString(names[i], r.X + 5, r.Y + 5, 1, Color.White);
                DrawString($"[{i+3}]", r.X + 75, r.Y + 35, 1, Color.White * 0.7f);
                float cd = _resourceManager.AbilityCooldowns[i];
                if (cd > 0) _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y + 45, (int)(100 * (cd / 45f)), 5), colors[i]); // Approx CD fill
            }

            // 3. Wave Info
            _spriteBatch.Draw(_pixel, new Rectangle(880, 15, 60, 50), Color.Black * 0.3f);
            DrawNumber(_waveManager.CurrentWave, 890, 25, 3, Color.White);

            // 4. Interaction Prompts (Bottom of HUD)
            DrawString("F: REPAIR HOVERED (10 SCRAP)   R: ROTATE PREVIEW", 20, 75, 1, Color.White * 0.8f);
        }

        private void DrawLabeledBar(string label, int x, int y, int width, float percent, Color color)
        {
            DrawString(label, x, y, 2, Color.White * 0.9f);
            _spriteBatch.Draw(_pixel, new Rectangle(x, y + 25, width, 30), Color.Black * 0.6f);
            _spriteBatch.Draw(_pixel, new Rectangle(x, y + 25, (int)(width * MathHelper.Clamp(percent, 0, 1)), 30), color);
            DrawBorder(_spriteBatch, _pixel, new Rectangle(x, y + 25, width, 30), 2, Color.White * 0.2f);
        }

        private void DrawHelpOverlay()
        {
            Rectangle bg = new Rectangle(50, 50, _virtualWidth - 100, _virtualHeight - 100);
            _spriteBatch.Draw(_pixel, bg, Color.Black * 0.95f);
            DrawBorder(_spriteBatch, _pixel, bg, 4, Color.Cyan * 0.5f);
            int x = 80, y = 80;
            DrawString("GAME INFO", x + 250, y, 4, Color.Cyan); y += 80;
            
            Action<string, Color> header = (txt, col) => { 
                DrawString(txt, x, y, 2, col); 
                DrawLine(_spriteBatch, _pixel, new Vector2(x, y + 20), new Vector2(x + 300, y + 20), 2, col * 0.5f); 
                y += 35; 
            };

            header("MAP HAZARDS", Color.Yellow);
            _spriteBatch.Draw(_textures["hazard_obstacle"], new Rectangle(x, y, 24, 24), Color.White);
            DrawString("OBSTACLE: UNBUILDABLE WALL", x + 35, y + 4, 2, Color.White); y += 30;
            
            _spriteBatch.Draw(_textures["hazard_volcanic"], new Rectangle(x, y, 24, 24), Color.White);
            DrawString("VOLCANIC: +50% UNIT HEAT GEN", x + 35, y + 4, 2, Color.White); y += 30;
            
            _spriteBatch.Draw(_textures["hazard_corrosive"], new Rectangle(x, y, 24, 24), Color.White);
            DrawString("CORROSIVE: 5 DPS TO ENEMIES", x + 35, y + 4, 2, Color.White); y += 50;

            header("INFRASTRUCTURE", Color.Yellow);
            _spriteBatch.Draw(_textures["player_base_core"], new Rectangle(x, y, 24, 24), Color.White);
            DrawString("CORE: POWER SOURCE & MAIN BASE", x + 35, y + 4, 2, Color.White); y += 30;
            
            _spriteBatch.Draw(_textures["building_power_plant"], new Rectangle(x, y, 24, 24), Color.White);
            DrawString("PLANT: EXTENDS LOGISTICS RANGE", x + 35, y + 4, 2, Color.White); y += 30;
            
            DrawString("UNPOWERED (RED DOT): 80% REGEN PENALTY", x, y, 2, Color.White); y += 50;

            header("COMBAT & PROGRESSION", Color.Yellow);
            DrawString("UNITS ATTACK IN 90 DEGREE FRONT ARC", x, y, 2, Color.White); y += 25;
            DrawString("GAIN XP -> RANK 2 -> SPEC (1=CRYO 2=AP)", x, y, 2, Color.White); y += 45;

            header("ENEMY THREATS", Color.Yellow);
            _spriteBatch.Draw(_textures["enemy_standard"], new Rectangle(x, y, 20, 20), Color.White);
            DrawString("STANDARD: BALANCED INFANTRY", x + 30, y + 2, 2, Color.White); y += 22;
            
            _spriteBatch.Draw(_textures["enemy_speedster"], new Rectangle(x, y, 20, 20), Color.White);
            DrawString("SPEEDSTER: FAST SCOUT UNIT", x + 30, y + 2, 2, Color.White); y += 22;

            _spriteBatch.Draw(_textures["enemy_tank"], new Rectangle(x, y, 20, 20), Color.White);
            DrawString("TANK: HEAVY ARMORED UNIT", x + 30, y + 2, 2, Color.White); y += 22;

            _spriteBatch.Draw(_textures["enemy_phaser"], new Rectangle(x, y, 20, 20), Color.White);
            DrawString("PHASER: GHOST (GORES CORE)", x + 30, y + 2, 2, Color.White); y += 22;

            _spriteBatch.Draw(_textures["enemy_support"], new Rectangle(x, y, 20, 20), Color.White);
            DrawString("SUPPORT: AOE ALLY HEALER", x + 30, y + 2, 2, Color.White); y += 22;
            
            _spriteBatch.Draw(_textures["enemy_striker"], new Rectangle(x, y, 20, 20), Color.White);
            DrawString("STRIKER: SIEGE (ATTACKS UNITS)", x + 30, y + 2, 2, Color.White); y += 22;
            
            _spriteBatch.Draw(_textures["enemy_harbinger"], new Rectangle(x, y, 20, 20), Color.White);
            DrawString("HARBINGER: BOSS + DEBUFF AURA", x + 30, y + 2, 2, Color.White); y += 40;

            header("RESOURCE ECONOMY", Color.Yellow);
            DrawString("ENERGY: PLACEMENT (25) - REGENS OVER TIME", x, y, 2, Color.White); y += 25;
            DrawString("SCRAP : SPECIALIZATION (40) - DROPPED BY FOES", x, y, 2, Color.White); y += 60;
            
            DrawString("PRESS SPACE OR CLICK ICON TO DISMISS", x + 180, bg.Height + 10, 2, Color.Cyan * 0.7f);
        }

        private Rectangle GetViewportRect()
        {
            int w = Window.ClientBounds.Width, h = Window.ClientBounds.Height;
            if (w <= 0 || h <= 0) return new Rectangle(0, 0, 1, 1);
            float vAsp = (float)_virtualWidth / _virtualHeight, aAsp = (float)w / h;
            int tW = w, tH = h, x = 0, y = 0;
            if (aAsp > vAsp) { tW = (int)(h * vAsp); x = (w - tW) / 2; }
            else { tH = (int)(w / vAsp); y = (h - tH) / 2; }
            return new Rectangle(x, y, tW, tH);
        }

        private void DrawString(string text, int x, int y, int size, Color color) {
            int curX = x; foreach (char c in text.ToUpper()) {
                if (c >= '0' && c <= '9') DrawDigit(c - '0', new Vector2(curX, y), size, color);
                else DrawChar(c, new Vector2(curX, y), size, color);
                curX += size * 7;
            }
        }

        private void DrawChar(char c, Vector2 pos, int size, Color color) {
            bool[,] segments = c switch {
                'A' => new[,] { { false, true, true, true, false }, { true, false, false, false, true }, { true, true, true, true, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true } },
                'B' => new[,] { { true, true, true, true, false }, { true, false, false, false, true }, { true, false, false, false, true }, { true, true, true, true, false }, { true, false, false, false, true }, { true, false, false, false, true }, { true, true, true, true, false } },
                'C' => new[,] { { false, true, true, true, true }, { true, false, false, false, false }, { true, false, false, false, false }, { true, false, false, false, false }, { true, false, false, false, false }, { true, false, false, false, false }, { false, true, true, true, true } },
                'D' => new[,] { { true, true, true, true, false }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, true, true, true, false } },
                'E' => new[,] { { true, true, true, true, true }, { true, false, false, false, false }, { true, false, false, false, false }, { true, true, true, true, false }, { true, false, false, false, false }, { true, false, false, false, false }, { true, true, true, true, true } },
                'F' => new[,] { { true, true, true, true, true }, { true, false, false, false, false }, { true, false, false, false, false }, { true, true, true, true, false }, { true, false, false, false, false }, { true, false, false, false, false }, { true, false, false, false, false } },
                'G' => new[,] { { false, true, true, true, true }, { true, false, false, false, false }, { true, false, false, false, false }, { true, false, true, true, true }, { true, false, false, false, true }, { true, false, false, false, true }, { false, true, true, true, true } },
                'H' => new[,] { { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, true, true, true, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true } },
                'I' => new[,] { { true, true, true, true, true }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { true, true, true, true, true } },
                'J' => new[,] { { false, false, false, false, true }, { false, false, false, false, true }, { false, false, false, false, true }, { false, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { false, true, true, true, false } },
                'K' => new[,] { { true, false, false, false, true }, { true, false, false, true, false }, { true, false, true, false, false }, { true, true, false, false, false }, { true, false, true, false, false }, { true, false, false, true, false }, { true, false, false, false, true } },
                'L' => new[,] { { true, false, false, false, false }, { true, false, false, false, false }, { true, false, false, false, false }, { true, false, false, false, false }, { true, false, false, false, false }, { true, false, false, false, false }, { true, true, true, true, true } },
                'M' => new[,] { { true, false, false, false, true }, { true, true, false, true, true }, { true, false, true, false, true }, { true, false, true, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true } },
                'N' => new[,] { { true, false, false, false, true }, { true, true, false, false, true }, { true, false, true, false, true }, { true, false, true, false, true }, { true, false, false, true, true }, { true, false, false, false, true }, { true, false, false, false, true } },
                'O' => new[,] { { false, true, true, true, false }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { false, true, true, true, false } },
                'P' => new[,] { { true, true, true, true, false }, { true, false, false, false, true }, { true, false, false, false, true }, { true, true, true, true, false }, { true, false, false, false, false }, { true, false, false, false, false }, { true, false, false, false, false } },
                'Q' => new[,] { { false, true, true, true, false }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, true, false, true }, { true, false, false, true, false }, { false, true, true, false, true } },
                'R' => new[,] { { true, true, true, true, false }, { true, false, false, false, true }, { true, false, false, false, true }, { true, true, true, true, false }, { true, false, true, false, false }, { true, false, false, true, false }, { true, false, false, false, true } },
                'S' => new[,] { { false, true, true, true, true }, { true, false, false, false, false }, { true, false, false, false, false }, { false, true, true, true, false }, { false, false, false, false, true }, { false, false, false, false, true }, { true, true, true, true, false } },
                'T' => new[,] { { true, true, true, true, true }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false } },
                'U' => new[,] { { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { false, true, true, true, false } },
                'V' => new[,] { { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { false, true, false, true, false }, { false, false, true, false, false } },
                'W' => new[,] { { true, false, false, false, true }, { true, false, false, false, true }, { true, false, false, false, true }, { true, false, true, false, true }, { true, false, true, false, true }, { true, true, false, true, true }, { true, false, false, false, true } },
                'X' => new[,] { { true, false, false, false, true }, { true, false, false, false, true }, { false, true, false, true, false }, { false, false, true, false, false }, { false, true, false, true, false }, { true, false, false, false, true }, { true, false, false, false, true } },
                'Y' => new[,] { { true, false, false, false, true }, { true, false, false, false, true }, { false, true, false, true, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false } },
                'Z' => new[,] { { true, true, true, true, true }, { false, false, false, false, true }, { false, false, false, true, false }, { false, false, true, false, false }, { false, true, false, false, false }, { true, false, false, false, false }, { true, true, true, true, true } },
                '?' => new[,] { { false, true, true, true, false }, { true, false, false, false, true }, { false, false, false, false, true }, { false, false, true, true, false }, { false, false, true, false, false }, { false, false, false, false, false }, { false, false, true, false, false } },
                ':' => new[,] { { false, false, false, false, false }, { false, false, true, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, true, false, false }, { false, false, false, false, false } },
                _ => new[,] { { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false } }
            };
            for (int y = 0; y < 7; y++) for (int x = 0; x < 5; x++) if (segments[y, x]) _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X + x * size, (int)pos.Y + y * size, size, size), color);
        }

        private void DrawNumber(int num, int x, int y, int size, Color color) {
            string s = num.ToString(); for (int i = 0; i < s.Length; i++) DrawDigit(s[i] - '0', new Vector2(x + i * (size * 6), y), size, color);
        }

        private void DrawDigit(int d, Vector2 pos, int sz, Color col) {
            bool[,] segs = d switch {
                0 => new[,] { { false, true, true, true, false }, { true, false, false, false, true }, { true, false, false, true, true }, { true, false, true, false, true }, { true, true, false, false, true }, { true, false, false, false, true }, { false, true, true, true, false } },
                1 => new[,] { { false, false, true, false, false }, { false, true, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, true, true, true, false } },
                2 => new[,] { { false, true, true, true, false }, { true, false, false, false, true }, { false, false, false, false, true }, { false, false, true, true, false }, { false, true, false, false, false }, { true, false, false, false, false }, { true, true, true, true, true } },
                3 => new[,] { { true, true, true, true, true }, { false, false, false, true, false }, { false, false, true, false, false }, { false, false, false, true, false }, { false, false, false, false, true }, { true, false, false, false, true }, { false, true, true, true, false } },
                4 => new[,] { { false, false, false, true, false }, { false, false, true, true, false }, { false, true, false, true, false }, { true, false, false, true, false }, { true, true, true, true, true }, { false, false, false, true, false }, { false, false, false, true, false } },
                5 => new[,] { { true, true, true, true, true }, { true, false, false, false, false }, { true, true, true, true, false }, { false, false, false, false, true }, { false, false, false, false, true }, { true, false, false, false, true }, { false, true, true, true, false } },
                6 => new[,] { { false, true, true, true, false }, { true, false, false, false, false }, { true, false, false, false, false }, { true, true, true, true, false }, { true, false, false, false, true }, { true, false, false, false, true }, { false, true, true, true, false } },
                7 => new[,] { { true, true, true, true, true }, { false, false, false, false, true }, { false, false, false, true, false }, { false, false, true, false, false }, { false, true, false, false, false }, { false, true, false, false, false }, { false, true, false, false, false } },
                8 => new[,] { { false, true, true, true, false }, { true, false, false, false, true }, { true, false, false, false, true }, { false, true, true, true, false }, { true, false, false, false, true }, { true, false, false, false, true }, { false, true, true, true, false } },
                9 => new[,] { { false, true, true, true, false }, { true, false, false, false, true }, { true, false, false, false, true }, { false, true, true, true, true }, { false, false, false, false, true }, { true, false, false, false, true }, { false, true, true, true, false } },
                _ => new[,] { { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false } }
            };
            for (int y = 0; y < 7; y++) for (int x = 0; x < 5; x++) if (segs[y, x]) _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X + x * sz, (int)pos.Y + y * sz, sz, sz), col);
        }

        private void DrawBorder(SpriteBatch sb, Texture2D px, Rectangle r, int thick, Color col) {
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, thick), col);
            sb.Draw(px, new Rectangle(r.X, r.Y + r.Height - thick, r.Width, thick), col);
            sb.Draw(px, new Rectangle(r.X, r.Y, thick, r.Height), col);
            sb.Draw(px, new Rectangle(r.X + r.Width - thick, r.Y, thick, r.Height), col);
        }

        private void DrawLine(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end, int thick, Color col) {
            Vector2 edge = end - start; float angle = (float)Math.Atan2(edge.Y, edge.X);
            sb.Draw(px, new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thick), null, col, angle, Vector2.Zero, SpriteEffects.None, 0);
        }
    }
}
