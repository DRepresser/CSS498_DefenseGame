using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using TacticalDefenseGame.Managers;
using TacticalDefenseGame.Models;
using TacticalDefenseGame.Entities;

namespace TacticalDefenseGame.Core
{
    public class TacticalDefenseGame : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        
        // Primitive rendering helpers
        private Texture2D _pixel;

        // Virtual Resolution / Scaling
        private RenderTarget2D _renderTarget;
        private int _virtualWidth;
        private int _virtualHeight;

        // HUD Constants
        private const int HudHeight = 60;

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

        // Game State
        private GameState _currentState;

        public TacticalDefenseGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Set internal virtual size based on grid + HUD
            _virtualWidth = GridManager.GridSize * GridManager.CellSize;
            _virtualHeight = GridManager.GridSize * GridManager.CellSize + HudHeight;

            _graphics.PreferredBackBufferWidth = _virtualWidth;
            _graphics.PreferredBackBufferHeight = _virtualHeight;
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

            _resourceManager = new ResourceManager(100f); // Start with 100 energy
            _entityManager = new EntityManager();
            _entityManager.Initialize(_gridManager, _resourceManager); // Subscribe to events
            _waveManager = new WaveManager();
            _telemetryManager = new TelemetryManager();
            _currentState = GameState.Gameplay;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _renderTarget = new RenderTarget2D(GraphicsDevice, _virtualWidth, _virtualHeight);
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboardState.IsKeyDown(Keys.Escape))
                Exit();

            if (_currentState == GameState.Gameplay)
            {
                // Handle Node Rotation
                if (keyboardState.IsKeyDown(Keys.R) && _lastKeyboardState.IsKeyUp(Keys.R))
                {
                    _currentFacing = (Direction)(((int)_currentFacing + 1) % 4);
                }

                var mouseState = Mouse.GetState();
                
                // Map screen mouse to virtual coordinates (Maintaining Aspect Ratio)
                Rectangle vp = GetViewportRect();
                float scale = (float)vp.Width / _virtualWidth;
                int vMouseX = (int)((mouseState.X - vp.X) / scale);
                int vMouseY = (int)((mouseState.Y - vp.Y) / scale);

                // Handle Info Icon Click
                if (mouseState.LeftButton == ButtonState.Pressed && _lastKeyboardState.IsKeyUp(Keys.None)) // Check for click
                {
                    if (vMouseX >= 900 && vMouseX <= 940 && vMouseY >= 10 && vMouseY <= 50)
                    {
                        if (DateTime.Now.Ticks / 10000 - _lastClickTicks > 200) // Debounce
                        {
                            _showHelp = !_showHelp;
                            _lastClickTicks = DateTime.Now.Ticks / 10000;
                        }
                    }
                }

                if (_showHelp)
                {
                    if (keyboardState.IsKeyDown(Keys.Space) || keyboardState.IsKeyDown(Keys.Enter))
                        _showHelp = false;
                }
                else
                {
                    int gridX = vMouseX / GridManager.CellSize;
                    int gridY = (vMouseY - HudHeight) / GridManager.CellSize;

                    if (mouseState.LeftButton == ButtonState.Pressed && vMouseY >= HudHeight)
                    {
                        if (_resourceManager.Energy >= 15f)
                        {
                            if (_gridManager.CanPlaceNode(gridX, gridY, _spawnPoint, _corePoint, _entityManager.ActiveEnemies))
                            {
                                _resourceManager.TrySpendEnergy(15f);
                                _telemetryManager.RecordEnergySpent(15f);
                                var node = _entityManager.GetNodeFromPool(new Point(gridX, gridY), _currentFacing);
                                _gridManager.PlaceNode(gridX, gridY, node, _corePoint);
                            }
                        }
                    }
                    if (mouseState.RightButton == ButtonState.Pressed && vMouseY >= HudHeight)
                    {
                        _gridManager.RemoveNode(gridX, gridY, _spawnPoint, _corePoint);
                    }

                    // Handle Node Specialization
                    if (vMouseY >= HudHeight)
                    {
                        var hoveredCell = _gridManager.GetCell(gridX, gridY);
                        if (hoveredCell != null && hoveredCell.OccupyingNode != null && hoveredCell.OccupyingNode.Rank >= 2 && hoveredCell.OccupyingNode.Specialization == NodeSpecialization.None)
                        {
                            if (keyboardState.IsKeyDown(Keys.D1) && _lastKeyboardState.IsKeyUp(Keys.D1))
                            {
                                if (_resourceManager.TrySpendScrap(30f))
                                    hoveredCell.OccupyingNode.Specialization = NodeSpecialization.Cryo;
                            }
                            else if (keyboardState.IsKeyDown(Keys.D2) && _lastKeyboardState.IsKeyUp(Keys.D2))
                            {
                                if (_resourceManager.TrySpendScrap(30f))
                                    hoveredCell.OccupyingNode.Specialization = NodeSpecialization.ArmorPiercing;
                            }
                        }
                    }

                    if (keyboardState.IsKeyDown(Keys.Space) && _lastKeyboardState.IsKeyUp(Keys.Space))
                    {
                        if (!_waveManager.IsWaveActive())
                        {
                            if (_waveManager.CurrentWave > 0)
                                _telemetryManager.EndWave(_resourceManager.CoreHealth);

                            _waveManager.StartNextWave();
                            _telemetryManager.StartWave(_waveManager.CurrentWave);
                        }
                    }

                    _gridManager.Update(gameTime);
                    _resourceManager.Update(gameTime);
                    int reachedCore = _entityManager.Update(gameTime, _spawnPoint, _corePoint);
                    if (reachedCore > 0)
                    {
                        float damage = reachedCore * 10f;
                        _resourceManager.TakeDamage(damage);
                        _telemetryManager.RecordDamageTaken(damage);
                    }

                    _waveManager.Update(gameTime, _entityManager, _gridManager, _spawnPoint, _corePoint);
                }

                if (_resourceManager.CoreHealth <= 0)
                {
                    _telemetryManager.EndWave(_resourceManager.CoreHealth);
                    _telemetryManager.ExportToJson("telemetry.json");
                    _currentState = GameState.GameOver;
                }
                else if (_waveManager.AllWavesComplete)
                {
                    _telemetryManager.EndWave(_resourceManager.CoreHealth);
                    _telemetryManager.ExportToJson("telemetry.json");
                    _currentState = GameState.Victory;
                }

                Window.Title = "Tactical Grid Defense";
            }
            else
            {
                if (keyboardState.IsKeyDown(Keys.Enter))
                {
                    Initialize(); // Restart
                }
            }

            _lastKeyboardState = keyboardState;

            base.Update(gameTime);
        }

        private long _lastClickTicks = 0;

        protected override void Draw(GameTime gameTime)
        {
            // 1. Draw to Virtual RenderTarget
            GraphicsDevice.SetRenderTarget(_renderTarget);
            GraphicsDevice.Clear(Color.Black);

            var mouseState = Mouse.GetState();
            Rectangle vp = GetViewportRect();
            float scale = (float)vp.Width / _virtualWidth;
            int vMouseX = (int)((mouseState.X - vp.X) / scale);
            int vMouseY = (int)((mouseState.Y - vp.Y) / scale);
            
            Point hoveredCell = new Point(vMouseX / GridManager.CellSize, (vMouseY - HudHeight) / GridManager.CellSize);

            _spriteBatch.Begin();
            DrawHUD();
            
            // Draw Help Icon
            Rectangle helpRect = new Rectangle(900, 10, 40, 40);
            _spriteBatch.Draw(_pixel, helpRect, Color.Gray * 0.4f);
            DrawBorder(_spriteBatch, _pixel, helpRect, 2, Color.White * 0.5f);
            DrawString("?", 912, 15, 3, Color.White);
            
            _spriteBatch.End();

            _spriteBatch.Begin(transformMatrix: Matrix.CreateTranslation(0, HudHeight, 0));
            _gridManager.Draw(_spriteBatch, _pixel, hoveredCell, _entityManager.ActiveEnemies);
            
            _spriteBatch.Draw(_pixel, new Rectangle(_spawnPoint.X * GridManager.CellSize, _spawnPoint.Y * GridManager.CellSize, GridManager.CellSize, GridManager.CellSize), Color.Green * 0.5f);
            _spriteBatch.Draw(_pixel, new Rectangle(_corePoint.X * GridManager.CellSize, _corePoint.Y * GridManager.CellSize, GridManager.CellSize, GridManager.CellSize), Color.Blue * 0.5f);
            
            _entityManager.Draw(_spriteBatch, _pixel);

            // Placement Preview
            if (!_showHelp && _currentState == GameState.Gameplay && vMouseY >= HudHeight)
            {
                int pGridX = vMouseX / GridManager.CellSize;
                int pGridY = (vMouseY - HudHeight) / GridManager.CellSize;
                
                if (pGridX >= 0 && pGridX < GridManager.GridSize && pGridY >= 0 && pGridY < GridManager.GridSize)
                {
                    bool canPlace = _gridManager.CanPlaceNode(pGridX, pGridY, _spawnPoint, _corePoint, _entityManager.ActiveEnemies);
                    Color previewColor = canPlace ? Color.White * 0.4f : Color.Red * 0.4f;
                    Vector2 previewPos = new Vector2(pGridX * GridManager.CellSize + GridManager.CellSize / 2, pGridY * GridManager.CellSize + GridManager.CellSize / 2);
                    _spriteBatch.Draw(_pixel, new Rectangle((int)previewPos.X - 15, (int)previewPos.Y - 15, 30, 30), previewColor);
                    
                    float angle = _currentFacing switch
                    {
                        Direction.Up => -MathHelper.PiOver2,
                        Direction.Right => 0,
                        Direction.Down => MathHelper.PiOver2,
                        Direction.Left => MathHelper.Pi,
                        _ => 0
                    };
                    Vector2 dirVec = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                    DrawLine(_spriteBatch, _pixel, previewPos, previewPos + dirVec * 20, 4, Color.Yellow * 0.6f);
                }
            }

            if (_currentState == GameState.GameOver)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _virtualWidth, _virtualHeight - HudHeight), Color.Red * 0.5f);
            }
            else if (_currentState == GameState.Victory)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _virtualWidth, _virtualHeight - HudHeight), Color.Gold * 0.5f);
            }
            _spriteBatch.End();

            // 2. Draw RenderTarget to Backbuffer (Uniform Scaling + Letterboxing)
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Viewport = new Viewport(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin();
            _spriteBatch.Draw(_renderTarget, vp, Color.White);
            _spriteBatch.End();

            // 3. Draw Help Overlay (Highest Layer, outside virtual space to stay sharp if needed, or inside for scale)
            if (_showHelp)
            {
                _spriteBatch.Begin();
                DrawHelpOverlay();
                _spriteBatch.End();
            }

            base.Draw(gameTime);
            }

            private Rectangle GetViewportRect()
            {
            int actualWidth = Window.ClientBounds.Width;
            int actualHeight = Window.ClientBounds.Height;

            if (actualWidth <= 0 || actualHeight <= 0) return new Rectangle(0, 0, 1, 1);

            float virtualAspect = (float)_virtualWidth / _virtualHeight;
            float actualAspect = (float)actualWidth / actualHeight;
            int targetWidth = actualWidth;
            int targetHeight = actualHeight;
            int x = 0;
            int y = 0;

            if (actualAspect > virtualAspect)
            {
                // Pillarbox
                targetWidth = (int)(actualHeight * virtualAspect);
                x = (actualWidth - targetWidth) / 2;
            }
            else
            {
                // Letterbox
                targetHeight = (int)(actualWidth / virtualAspect);
                y = (actualHeight - targetHeight) / 2;
            }

            return new Rectangle(x, y, targetWidth, targetHeight);
        }

        private void DrawHelpOverlay()
        {
            Rectangle bg = new Rectangle(50, 100, _virtualWidth - 100, _virtualHeight - 200);
            _spriteBatch.Draw(_pixel, bg, Color.Black * 0.9f);
            DrawBorder(_spriteBatch, _pixel, bg, 4, Color.Gray);

            int x = 80; int y = 130;
            DrawString("GAME INFO", x, y, 4, Color.White); y += 60;

            // 1. Hazards
            DrawString("MAP HAZARDS", x, y, 2, Color.Yellow); y += 30;
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 20, 20), Color.Black); DrawString("OBSTACLE-WALL", x + 30, y, 2, Color.White); y += 25;
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 20, 20), Color.OrangeRed * 0.6f); DrawString("VOLCANIC-HEAT UP", x + 30, y, 2, Color.White); y += 25;
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 20, 20), Color.DarkGreen * 0.6f); DrawString("CORROSIVE-DMG ENEMY", x + 30, y, 2, Color.White); y += 40;

            // 2. Power Grid
            DrawString("INFRASTRUCTURE", x, y, 2, Color.Yellow); y += 30;
            DrawString("CONNECT NODES TO CORE (BLUE)", x, y, 2, Color.White); y += 25;
            DrawString("UNPOWERED (RED DOT) = 80% REGEN PENALTY", x, y, 2, Color.White); y += 40;

            // 3. Combat
            DrawString("COMBAT & PROGRESSION", x, y, 2, Color.Yellow); y += 30;
            DrawString("UNIT ATTACK IN 90 DEGREE ARC", x, y, 2, Color.White); y += 25;
            DrawString("XP -> RANK 2 -> SPEC (1=CRYO 2=AP)", x, y, 2, Color.White); y += 40;

            // 4. Enemy
            DrawString("ENEMY TYPES", x, y, 2, Color.Yellow); y += 30;
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 15, 15), Color.LimeGreen); DrawString("SUPPORT-HEALER", x + 25, y, 2, Color.White); y += 25;
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 15, 15), Color.Crimson); DrawString("STRIKER-SIEGE UNIT", x + 25, y, 2, Color.White); y += 40;

            // 5. Economy
            DrawString("RESOURCE ECONOMY", x, y, 2, Color.Yellow); y += 30;
            DrawString("ENERGY:BUILDING (15)  SCRAP:SPEC (30)", x, y, 2, Color.White); y += 50;

            DrawString("PRESS SPACE OR CLICK ICON TO CLOSE", x, bg.Height + 60, 2, Color.Gray);
        }

        private void DrawString(string text, int x, int y, int size, Color color)
        {
            int curX = x;
            foreach (char c in text.ToUpper())
            {
                if (c >= '0' && c <= '9') DrawDigit(c - '0', new Vector2(curX, y), size, color);
                else DrawChar(c, new Vector2(curX, y), size, color);
                curX += size * 5;
            }
        }

        private void DrawChar(char c, Vector2 pos, int size, Color color)
        {
            bool[,] segments = c switch
            {
                'A' => new[,] { { false, true, false }, { true, false, true }, { true, true, true }, { true, false, true }, { true, false, true } },
                'B' => new[,] { { true, true, false }, { true, false, true }, { true, true, false }, { true, false, true }, { true, true, false } },
                'C' => new[,] { { true, true, true }, { true, false, false }, { true, false, false }, { true, false, false }, { true, true, true } },
                'D' => new[,] { { true, true, false }, { true, false, true }, { true, false, true }, { true, false, true }, { true, true, false } },
                'E' => new[,] { { true, true, true }, { true, false, false }, { true, true, false }, { true, false, false }, { true, true, true } },
                'F' => new[,] { { true, true, true }, { true, false, false }, { true, true, false }, { true, false, false }, { true, false, false } },
                'G' => new[,] { { true, true, true }, { true, false, false }, { true, false, true }, { true, false, true }, { true, true, true } },
                'H' => new[,] { { true, false, true }, { true, false, true }, { true, true, true }, { true, false, true }, { true, false, true } },
                'I' => new[,] { { true, true, true }, { false, true, false }, { false, true, false }, { false, true, false }, { true, true, true } },
                'J' => new[,] { { false, false, true }, { false, false, true }, { false, false, true }, { true, false, true }, { true, true, true } },
                'K' => new[,] { { true, false, true }, { true, false, true }, { true, true, false }, { true, false, true }, { true, false, true } },
                'L' => new[,] { { true, false, false }, { true, false, false }, { true, false, false }, { true, false, false }, { true, true, true } },
                'M' => new[,] { { true, false, true }, { true, true, true }, { true, false, true }, { true, false, true }, { true, false, true } },
                'N' => new[,] { { true, true, true }, { true, false, true }, { true, false, true }, { true, false, true }, { true, false, true } },
                'O' => new[,] { { true, true, true }, { true, false, true }, { true, false, true }, { true, false, true }, { true, true, true } },
                'P' => new[,] { { true, true, true }, { true, false, true }, { true, true, true }, { true, false, false }, { true, false, false } },
                'Q' => new[,] { { true, true, true }, { true, false, true }, { true, false, true }, { true, true, true }, { false, false, true } },
                'R' => new[,] { { true, true, true }, { true, false, true }, { true, true, false }, { true, false, true }, { true, false, true } },
                'S' => new[,] { { true, true, true }, { true, false, false }, { true, true, true }, { false, false, true }, { true, true, true } },
                'T' => new[,] { { true, true, true }, { false, true, false }, { false, true, false }, { false, true, false }, { false, true, false } },
                'U' => new[,] { { true, false, true }, { true, false, true }, { true, false, true }, { true, false, true }, { true, true, true } },
                'V' => new[,] { { true, false, true }, { true, false, true }, { true, false, true }, { true, false, true }, { false, true, false } },
                'W' => new[,] { { true, false, true }, { true, false, true }, { true, false, true }, { true, true, true }, { true, false, true } },
                'X' => new[,] { { true, false, true }, { true, false, true }, { false, true, false }, { true, false, true }, { true, false, true } },
                'Y' => new[,] { { true, false, true }, { true, false, true }, { true, true, true }, { false, true, false }, { false, true, false } },
                'Z' => new[,] { { true, true, true }, { false, false, true }, { false, true, false }, { true, false, false }, { true, true, true } },
                '?' => new[,] { { true, true, true }, { false, false, true }, { false, true, true }, { false, false, false }, { false, true, false } },
                ':' => new[,] { { false, false, false }, { false, true, false }, { false, false, false }, { false, true, false }, { false, false, false } },
                '-' => new[,] { { false, false, false }, { false, false, false }, { true, true, true }, { false, false, false }, { false, false, false } },
                '(' => new[,] { { false, true, false }, { true, false, false }, { true, false, false }, { true, false, false }, { false, true, false } },
                '=' => new[,] { { false, false, false }, { true, true, true }, { false, false, false }, { true, true, true }, { false, false, false } },
                _ => new[,] { { false, false, false }, { false, false, false }, { false, false, false }, { false, false, false }, { false, false, false } }
            };

            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    if (segments[y, x])
                    {
                        _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X + x * size, (int)pos.Y + y * size, size, size), color);
                    }
                }
            }
        }

        private void DrawHUD()
        {
            // HUD Background
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, HudHeight), Color.DarkSlateGray);
            _spriteBatch.Draw(_pixel, new Rectangle(0, HudHeight - 2, _graphics.PreferredBackBufferWidth, 2), Color.Black * 0.5f);

            // 1. Core HP Bar
            DrawLabeledBar("CORE HP", 10, 10, 150, _resourceManager.CoreHealth / 100f, Color.Crimson);
            DrawNumber((int)_resourceManager.CoreHealth, 120, 10, 2, Color.White);

            // 2. Energy Bar
            DrawLabeledBar("ENERGY", 170, 10, 150, _resourceManager.Energy / 200f, Color.Cyan); 
            DrawNumber((int)_resourceManager.Energy, 280, 10, 2, Color.White);

            // 3. Scrap Counter
            DrawLabeledBar("SCRAP", 330, 10, 100, _resourceManager.Scrap / 300f, Color.Gold);
            DrawNumber((int)_resourceManager.Scrap, 400, 10, 2, Color.White);

            // 4. Wave Counter
            _spriteBatch.Draw(_pixel, new Rectangle(450, 10, 140, HudHeight - 20), Color.Black * 0.3f);
            DrawNumber(_waveManager.CurrentWave, 460, 20, 3, Color.White);
            _spriteBatch.Draw(_pixel, new Rectangle(500, 25, 20, 10), Color.White * 0.5f); // Slash separator block
            DrawNumber(10, 530, 20, 3, Color.White * 0.7f);
        }

        private void DrawNumber(int number, int x, int y, int size, Color color)
        {
            string s = number.ToString();
            for (int i = 0; i < s.Length; i++)
            {
                int digit = s[i] - '0';
                DrawDigit(digit, new Vector2(x + i * (size * 4), y), size, color);
            }
        }

        private void DrawDigit(int digit, Vector2 pos, int size, Color color)
        {
            bool[,] segments = digit switch
            {
                0 => new[,] { { true, true, true }, { true, false, true }, { true, false, true }, { true, false, true }, { true, true, true } },
                1 => new[,] { { false, true, false }, { false, true, false }, { false, true, false }, { false, true, false }, { false, true, false } },
                2 => new[,] { { true, true, true }, { false, false, true }, { true, true, true }, { true, false, false }, { true, true, true } },
                3 => new[,] { { true, true, true }, { false, false, true }, { true, true, true }, { false, false, true }, { true, true, true } },
                4 => new[,] { { true, false, true }, { true, false, true }, { true, true, true }, { false, false, true }, { false, false, true } },
                5 => new[,] { { true, true, true }, { true, false, false }, { true, true, true }, { false, false, true }, { true, true, true } },
                6 => new[,] { { true, true, true }, { true, false, false }, { true, true, true }, { true, false, true }, { true, true, true } },
                7 => new[,] { { true, true, true }, { false, false, true }, { false, false, true }, { false, false, true }, { false, false, true } },
                8 => new[,] { { true, true, true }, { true, false, true }, { true, true, true }, { true, false, true }, { true, true, true } },
                9 => new[,] { { true, true, true }, { true, false, true }, { true, true, true }, { false, false, true }, { true, true, true } },
                _ => new[,] { { false, false, false }, { false, false, false }, { false, false, false }, { false, false, false }, { false, false, false } }
            };

            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    if (segments[y, x])
                    {
                        _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X + x * size, (int)pos.Y + y * size, size, size), color);
                    }
                }
            }
        }

        private void DrawLabeledBar(string label, int x, int y, int width, float percent, Color color)
        {
            // Draw Label using bitmapped font
            DrawString(label, x, y, 2, Color.White * 0.9f);
            
            // Bar Background
            _spriteBatch.Draw(_pixel, new Rectangle(x, y + 18, width, 22), Color.Black * 0.6f);
            // Bar Fill
            int fillWidth = (int)(width * MathHelper.Clamp(percent, 0, 1));
            _spriteBatch.Draw(_pixel, new Rectangle(x, y + 18, fillWidth, 22), color);
            // Bar Border
            DrawBorder(_spriteBatch, _pixel, new Rectangle(x, y + 18, width, 22), 1, Color.White * 0.2f);
        }

        private void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }

        private void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, int thickness, Color color)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            spriteBatch.Draw(pixel, 
                new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thickness),
                null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
        }
    }
}
