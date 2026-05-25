using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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

            // Set window size based on grid
            _graphics.PreferredBackBufferWidth = GridManager.GridSize * GridManager.CellSize;
            _graphics.PreferredBackBufferHeight = GridManager.GridSize * GridManager.CellSize;
        }

        protected override void Initialize()
        {
            _gridManager = new GridManager();
            _entityManager = new EntityManager();
            _entityManager.Initialize(_gridManager); // Subscribe to events
            _resourceManager = new ResourceManager(100f); // Start with 100 energy
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
                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    int gridX = mouseState.X / GridManager.CellSize;
                    int gridY = mouseState.Y / GridManager.CellSize;

                    if (_resourceManager.Energy >= 25f) // Cost of node
                    {
                        if (_gridManager.CanPlaceNode(gridX, gridY, _spawnPoint, _corePoint))
                        {
                            _resourceManager.TrySpendEnergy(25f);
                            _telemetryManager.RecordEnergySpent(25f);
                            var node = _entityManager.GetNodeFromPool(new Point(gridX, gridY), _currentFacing);
                            _gridManager.PlaceNode(gridX, gridY, node);
                            // Node is added to EntityManager via Event
                        }
                    }
                }
                if (mouseState.RightButton == ButtonState.Pressed)
                {
                    int gridX = mouseState.X / GridManager.CellSize;
                    int gridY = mouseState.Y / GridManager.CellSize;
                    _gridManager.RemoveNode(gridX, gridY, _spawnPoint, _corePoint);
                    // Node is removed from EntityManager via Event
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
                int reachedCore = _entityManager.Update(gameTime);
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

                Window.Title = $"HP: {(int)_resourceManager.CoreHealth} | Energy: {(int)_resourceManager.Energy} | Wave: {_waveManager.CurrentWave} | Facing: {_currentFacing}";
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
            Point hoveredCell = new Point(mouseState.X / GridManager.CellSize, mouseState.Y / GridManager.CellSize);

            _spriteBatch.Begin();

            _gridManager.Draw(_spriteBatch, _pixel, hoveredCell, _entityManager.ActiveEnemies);
            
            // Highlight Spawn and Core
            _spriteBatch.Draw(_pixel, new Rectangle(_spawnPoint.X * GridManager.CellSize, _spawnPoint.Y * GridManager.CellSize, GridManager.CellSize, GridManager.CellSize), Color.Green * 0.5f);
            _spriteBatch.Draw(_pixel, new Rectangle(_corePoint.X * GridManager.CellSize, _corePoint.Y * GridManager.CellSize, GridManager.CellSize, GridManager.CellSize), Color.Blue * 0.5f);
            
            _entityManager.Draw(_spriteBatch, _pixel);

            if (_currentState == GameState.GameOver)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight), Color.Red * 0.5f);
                Window.Title = "GAME OVER! Press ENTER to Restart";
            }
            else if (_currentState == GameState.Victory)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight), Color.Gold * 0.5f);
                Window.Title = "VICTORY! Press ENTER to Restart";
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
