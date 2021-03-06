/*

Copyright 2015 20tab S.r.l.
Copyright 2015 Aiv S.r.l.

*/

using System;
using System.Windows.Forms;
using System.Threading;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Media;

namespace Aiv.Engine
{
	public class Engine
	{
		
		public int fps;

		public int width;
		public int height;

		public Dictionary<string, GameObject> objects;
		public Dictionary<string, Asset> assets;

		// this is constantly filled with keyboard status
		private Dictionary<Keys, bool> keyboardTable;

		private List<GameObject> objectsToRender;

		public int startTicks;

		public int ticks {
			get {
				return Environment.TickCount;
			}
		}

		public delegate void BeforeUpdateEventHandler (object sender);

		public event BeforeUpdateEventHandler OnBeforeUpdate;

		public delegate void AfterUpdateEventHandler (object sender);

		public event AfterUpdateEventHandler OnAfterUpdate;

			
		// when true the renderling list must be rebuilt
		bool dirtyObjects;

		protected Bitmap workingBitmap;
		public Graphics workingGraphics;

		public bool isGameRunning = false;

		private Random random;

		public bool debugCollisions;

		public Mouse mouse;
		public Joystick[] joysticks;

		private class MainWindow : Form
		{

			public Graphics windowGraphics;
			public PictureBox pbox;

			public MainWindow ()
			{

				StartPosition = FormStartPosition.CenterScreen;
				FormBorderStyle = FormBorderStyle.FixedSingle;
				MaximizeBox = false;
				MinimizeBox = false;

                
				this.SetStyle (ControlStyles.AllPaintingInWmPaint, true);
				this.SetStyle (ControlStyles.OptimizedDoubleBuffer, true);
				this.SetStyle (ControlStyles.UserPaint, false);
				this.SetStyle (ControlStyles.FixedWidth, true);
				this.SetStyle (ControlStyles.FixedHeight, true);

				this.pbox = new PictureBox ();
				this.Controls.Add (pbox);

				this.windowGraphics = pbox.CreateGraphics ();
				this.windowGraphics.CompositingMode = CompositingMode.SourceCopy;
				this.windowGraphics.CompositingQuality = CompositingQuality.HighSpeed;
				this.windowGraphics.SmoothingMode = SmoothingMode.None;
				this.windowGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;
				this.windowGraphics.TextRenderingHint = TextRenderingHint.SystemDefault;
				this.windowGraphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
			}
		}

		private MainWindow window;

		protected void Initialize (string windowName, int width, int height, int fps)
		{
			

			this.width = width;
			this.height = height;
			this.fps = fps;


			this.workingBitmap = new Bitmap (width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			this.workingGraphics = Graphics.FromImage (this.workingBitmap);
			this.workingGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
			this.workingGraphics.CompositingQuality = CompositingQuality.HighSpeed;
			this.workingGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;
			this.workingGraphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;

			// create dictionaries
			this.objects = new Dictionary<string, GameObject> ();
			this.objectsToRender = new List<GameObject> ();
			this.assets = new Dictionary<string, Asset> ();
			this.keyboardTable = new Dictionary<Keys, bool> ();

			this.random = new Random (Guid.NewGuid ().GetHashCode ());

			this.joysticks = new Joystick[8];

		}

		protected Engine ()
		{
		}

		public Engine (string windowName, int width, int height, int fps)
		{
			

			this.Initialize (windowName, width, height, fps);

			this.window = new MainWindow ();
			this.window.Text = windowName;
			this.window.Size = new Size (width, height);
			Size clientSize = this.window.ClientSize;
			int deltaW = width - clientSize.Width;
			int deltaH = height - clientSize.Height;
			this.window.Size = new Size (width + deltaW, height + deltaH);
			this.window.pbox.Size = new Size (width, height);
			this.window.KeyDown += new KeyEventHandler (this.KeyDown);
			this.window.KeyUp += new KeyEventHandler (this.KeyUp);
			this.mouse = new Mouse (this);

		}


		public void DestroyAllObjects ()
		{
			foreach (GameObject obj in this.objects.Values) {
				obj.Destroy ();
			}
			// redundant now, could be useful in the future
			this.objects.Clear ();
		}

		protected void GameUpdate (int startTick)
		{
			

			if (this.OnBeforeUpdate != null)
				OnBeforeUpdate (this);

			this.workingGraphics.Clear (Color.Black);

			foreach (GameObject obj in this.objectsToRender) {
				obj.deltaTicks = startTick - obj.ticks;
				obj.ticks = startTick;
				if (!obj.enabled)
					continue;
				obj.Draw ();
				if (this.debugCollisions) {
					Pen green = new Pen (Color.Green);
					if (obj.hitBoxes != null) {
						foreach (GameObject.HitBox hitBox in obj.hitBoxes.Values) {
							this.workingGraphics.DrawRectangle (green, obj.x + hitBox.x, obj.y + hitBox.y, hitBox.width, hitBox.height);
						}
					}
				}
			}

			if (this.OnAfterUpdate != null)
				OnAfterUpdate (this);



			if (this.dirtyObjects) {
				this.objectsToRender.Clear ();
				foreach (GameObject obj in this.objects.Values.OrderBy(o=>o.order)) {
					this.objectsToRender.Add (obj);
				}
				this.dirtyObjects = false;
			}
		}

		public virtual void Run ()
		{

			this.window.Show ();

			isGameRunning = true;
            
			// compute update frequency
			int freq = 1000 / this.fps;
			this.startTicks = this.ticks;


			while (isGameRunning) {
				
				int startTick = this.ticks;

				Application.DoEvents ();

				this.GameUpdate (startTick);

				this.window.pbox.Image = this.workingBitmap;

				int endTick = this.ticks;

				// check if we need to slowdown
				if (endTick - startTick < freq) {
					Thread.Sleep (freq - (endTick - startTick));
				}
			}
		}

		/*
		 * 
		 * Asset's management
		 * 
		 */

		public void LoadAsset (string name, Asset asset)
		{
			asset.engine = this;
			this.assets [name] = asset;
		}

		public Asset GetAsset (string name)
		{
			return this.assets [name];
		}

		/* 
		 * 
		 * GameObject's management
		 * 
		 */

		public void SpawnObject (string name, GameObject obj)
		{
			obj.name = name;
			obj.engine = this;
			obj.enabled = true;
			this.objects [name] = obj;
			obj.Initialize ();
			// force the rendering list to be rebuilt
			this.dirtyObjects = true;
		}

		public void RemoveObject (GameObject obj)
		{
			this.objects.Remove (obj.name);
			this.dirtyObjects = true;
		}

		/*
		 * 
		 * 
		 * Utility functions
		 *
		 */

		public int Random (int start, int end)
		{
			return this.random.Next (start, end);
		}

		public virtual void PlaySound (string assetName)
		{
			SoundPlayer soundPlayer = new SoundPlayer (this.GetAsset (assetName).fileName);
			soundPlayer.Play ();
		}

		public virtual void PlaySoundLoop (string assetName)
		{
			SoundPlayer soundPlayer = new SoundPlayer (this.GetAsset (assetName).fileName);
			soundPlayer.PlayLooping ();
		}

		/*
		 * 
		 * Keyboard management
		 * 
		 */

		private void KeyDown (object sender, KeyEventArgs e)
		{
			this.keyboardTable [e.KeyCode] = true;
		}

		private void KeyUp (object sender, KeyEventArgs e)
		{
			this.keyboardTable [e.KeyCode] = false;
		}

		public virtual bool IsKeyDown (int key)
		{
			Keys _key = (Keys) key;
			
			if (!keyboardTable.ContainsKey (_key)) {
				return false;
			}
			return this.keyboardTable [_key];
		}

		public bool IsKeyDown (Keys key)
		{
			return this.IsKeyDown ((int)key);
		}

		/*
		 * 
		 * 
		 * Mouse management
		 * 
		 */

		public class Mouse
		{

			public Engine engine;
			public bool left;
			public bool right;
			public bool middle;
			public int wheel;

			public Mouse (Engine engine)
			{
				this.engine = engine;
				this.engine.window.pbox.MouseDown += new MouseEventHandler (this.MouseDown);
				this.engine.window.pbox.MouseUp += new MouseEventHandler (this.MouseUp);
				this.engine.window.MouseWheel += new MouseEventHandler (this.MouseWheel);
			}

			public int x {
				get {
					return Cursor.Position.X - this.engine.window.Location.X;
				}
			}

			public int y {
				get {
					return Cursor.Position.Y - this.engine.window.Location.Y;
				}
			}

			public int screenX {
				get {
					return Cursor.Position.X;
				}
			}

			public int screenY {
				get {
					return Cursor.Position.Y;
				}
			}

			private void MouseDown (object sender, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
					this.left = true;
				if (e.Button == MouseButtons.Right)
					this.right = true;
				if (e.Button == MouseButtons.Middle)
					this.middle = true;
			}

			private void MouseUp (object sender, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
					this.left = false;
				if (e.Button == MouseButtons.Right)
					this.right = false;
				if (e.Button == MouseButtons.Middle)
					this.middle = false;
			}

			private void MouseWheel (object sender, MouseEventArgs e)
			{
				wheel = e.Delta;
			}

		}


		/*
		 * 
		 * 
		 *  Joystick management (platform specific, expects initialization from Aiv.Engine.Input)
		 * 
		 */

		public class Joystick
		{
			public string name;
			public int x;
			public int y;
			public bool[] buttons;
			public long id;
			public int index;

			public Joystick ()
			{
				// max 20 buttons
				this.buttons = new bool[20];
			}

			public bool anyButton ()
			{
				foreach (bool pressed in this.buttons) {
					if (pressed)
						return true;
				}
				return false;
			}
		}
	}
}

