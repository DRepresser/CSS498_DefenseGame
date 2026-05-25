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

        // Game State
        private GameState _currentState;

        public TacticalDefenseGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Set window size based on grid + HUD
            _graphics.PreferredBackBufferWidth = GridManager.GridSize * GridManager.CellSize;
            _graphics.PreferredBackBufferHeight = GridManager.GridSize * GridManager.CellSize + HudHeight;
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
                int gridX = mouseState.X / GridManager.CellSize;
                int gridY = (mouseState.Y - HudHeight) / GridManager.CellSize;

                if (mouseState.LeftButton == ButtonState.Pressed && mouseState.Y >= HudHeight)
                {
                    if (_resourceManager.Energy >= 15f) // Reduced Cost: 15
                    {
                        if (_gridManager.CanPlaceNode(gridX, gridY, _spawnPoint, _corePoint, _entityManager.ActiveEnemies))
                        {
                            _resourceManager.TrySpendEnergy(15f);
                            _telemetryManager.RecordEnergySpent(15f);
                            var node = _entityManager.GetNodeFromPool(new Point(gridX, gridY), _currentFacing);
                            _gridManager.PlaceNode(gridX, gridY, node, _corePoint);
                            // Node is added to EntityManager via Event
                        }
                    }
                }
                if (mouseState.RightButton == ButtonState.Pressed && mouseState.Y >= HudHeight)
                {
                    _gridManager.RemoveNode(gridX, gridY, _spawnPoint, _corePoint);
                    // Node is removed from EntityManager via Event
                }

                // Handle Node Specialization
                if (mouseState.Y >= HudHeight)
                {
                    var hoveredCell = _gridManager.GetCell(gridX, gridY);
                    if (hoveredCell != null && hoveredCell.OccupyingNode != null && hoveredCell.OccupyingNode.Rank >= 2 && hoveredCell.OccupyingNode.Specialization == NodeSpecialization.None)
                    {
                        if (keyboardState.IsKeyDown(Keys.D1) && _lastKeyboardState.IsKeyUp(Keys.D1))
                        {
                            if (_resourceManager.TrySpendScrap(30f)) // Reduced Cost: 30
                            {
                                hoveredCell.OccupyingNode.Specialization = NodeSpecialization.Cryo;
                            }
                        }
                        else if (keyboardState.IsKeyDown(Keys.D2) && _lastKeyboardState.IsKeyUp(Keys.D2))
                        {
                            if (_resourceManager.TrySpendScrap(30f)) // Reduced Cost: 30
                            {
                                hoveredCell.OccupyingNode.Specialization = NodeSpecialization.ArmorPiercing;
                            }
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
                    _resourceManager.TakeDamage(damage); // 10 damage per enemy
                    _telemetryManager.RecordDamageTaken(damage);
                }

                _waveManager.Update(gameTime, _entityManager, _gridManager, _spawnPoint, _corePoint);

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

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            var mouseState = Mouse.GetState();
            Point hoveredCell = new Point(mouseState.X / GridManager.CellSize, (mouseState.Y - HudHeight) / GridManager.CellSize);

            _spriteBatch.Begin();

            // 1. Draw HUD
            DrawHUD();

            // 2. Draw Game World (shifted by HudHeight)
            // Using a Matrix for translation makes drawing everything easier
            _spriteBatch.End();
            _spriteBatch.Begin(transformMatrix: Matrix.CreateTranslation(0, HudHeight, 0));

            _gridManager.Draw(_spriteBatch, _pixel, hoveredCell, _entityManager.ActiveEnemies);
            
            // Highlight Spawn and Core
            _spriteBatch.Draw(_pixel, new Rectangle(_spawnPoint.X * GridManager.CellSize, _spawnPoint.Y * GridManager.CellSize, GridManager.CellSize, GridManager.CellSize), Color.Green * 0.5f);
            _spriteBatch.Draw(_pixel, new Rectangle(_corePoint.X * GridManager.CellSize, _corePoint.Y * GridManager.CellSize, GridManager.CellSize, GridManager.CellSize), Color.Blue * 0.5f);
            
            _entityManager.Draw(_spriteBatch, _pixel);

            // 3. Draw Placement Preview
            if (_currentState == GameState.Gameplay && mouseState.Y >= HudHeight)
            {
                int pGridX = mouseState.X / GridManager.CellSize;
                int pGridY = (mouseState.Y - HudHeight) / GridManager.CellSize;
                
                if (pGridX >= 0 && pGridX < GridManager.GridSize && pGridY >= 0 && pGridY < GridManager.GridSize)
                {
                    bool canPlace = _gridManager.CanPlaceNode(pGridX, pGridY, _spawnPoint, _corePoint, _entityManager.ActiveEnemies);
                    Color previewColor = canPlace ? Color.White * 0.4f : Color.Red * 0.4f;
                    
                    Vector2 previewPos = new Vector2(pGridX * GridManager.CellSize + GridManager.CellSize / 2, pGridY * GridManager.CellSize + GridManager.CellSize / 2);
                    
                    // Draw Base
                    _spriteBatch.Draw(_pixel, new Rectangle((int)previewPos.X - 15, (int)previewPos.Y - 15, 30, 30), previewColor);
                    
                    // Draw Direction Indicator
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
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight - HudHeight), Color.Red * 0.5f);
            }
            else if (_currentState == GameState.Victory)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight - HudHeight), Color.Gold * 0.5f);
            }

            _spriteBatch.End();

            base.Draw(gameTime);
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
            // Label (Simplified: just a colored block for now as we don't have fonts loaded)
            // In a real scenario, use SpriteFont.
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, 15), Color.Black * 0.4f);
            
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
