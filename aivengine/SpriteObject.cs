﻿using System;

namespace Aiv.Engine
{
	public class SpriteObject : GameObject
	{

		public SpriteAsset currentSprite;

		public SpriteObject (string name, int x, int y) : base(name)
		{
		}

		public override void Update() {
			base.Update();
			if (this.currentSprite != null)
				this.engine.workingGraphics.DrawImage (this.currentSprite.sprite, this.x, this.y);
		}
	}
}

