using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace IFWillyCG;

public sealed class ModEntry : Mod
{
    private Texture2D? cg;
    private bool visible;

    public override void Entry(IModHelper helper)
    {
        this.cg = helper.ModContent.Load<Texture2D>("assets/IFWillyKissCGFull.png");
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.visible = false;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        object? ev = GetStaticMember("StardewValley.Game1, Stardew Valley", "CurrentEvent")
                     ?? GetStaticMember("StardewValley.Game1, Stardew Valley", "currentEvent");

        if (ev is null)
        {
            this.visible = false;
            return;
        }

        string[] commands = GetCommands(ev);
        if (commands.Length == 0 || !commands.Any(p => p.Contains("겁대가리 없는 건 알고 있었는데", StringComparison.Ordinal)))
        {
            this.visible = false;
            return;
        }

        int? index = GetIntMember(ev, "CurrentCommand", "currentCommand", "currentCommandIndex", "CurrentCommandIndex");
        if (index is null || index < 0 || index >= commands.Length)
            return;

        string command = commands[index.Value];
        if (command.StartsWith("pause 317", StringComparison.Ordinal))
            this.visible = true;
        else if (command.StartsWith("pause 319", StringComparison.Ordinal))
            this.visible = false;
    }

    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!this.visible || this.cg is null)
            return;

        Rectangle screen = e.SpriteBatch.GraphicsDevice.Viewport.Bounds;
        if (screen.Width <= 0 || screen.Height <= 0)
            return;

        // Hide the game world completely even when the monitor isn't 16:9.
        e.SpriteBatch.Draw(this.cg, screen, Color.Black);

        // Keep the full illustration visible without distortion or cropping.
        float scale = Math.Min(screen.Width / (float)this.cg.Width, screen.Height / (float)this.cg.Height);
        int width = Math.Max(1, (int)MathF.Round(this.cg.Width * scale));
        int height = Math.Max(1, (int)MathF.Round(this.cg.Height * scale));
        int x = screen.X + (screen.Width - width) / 2;
        int y = screen.Y + (screen.Height - height) / 2;

        e.SpriteBatch.Draw(this.cg, new Rectangle(x, y, width, height), Color.White);
    }

    private static object? GetStaticMember(string typeName, string name)
    {
        Type? type = Type.GetType(typeName);
        if (type is null)
            return null;
        return type.GetProperty(name)?.GetValue(null) ?? type.GetField(name)?.GetValue(null);
    }

    private static object? GetMember(object obj, string name)
    {
        Type type = obj.GetType();
        return type.GetProperty(name)?.GetValue(obj) ?? type.GetField(name)?.GetValue(obj);
    }

    private static int? GetIntMember(object obj, params string[] names)
    {
        foreach (string name in names)
        {
            object? value = GetMember(obj, name);
            if (value is int number)
                return number;
        }
        return null;
    }

    private static string[] GetCommands(object ev)
    {
        object? raw = GetMember(ev, "eventCommands") ?? GetMember(ev, "EventCommands");
        if (raw is null)
            return Array.Empty<string>();

        if (raw is string[] array)
            return array;

        if (raw is IEnumerable enumerable)
        {
            List<string> result = new();
            foreach (object? item in enumerable)
            {
                if (item is string text)
                    result.Add(text);
            }
            return result.ToArray();
        }

        return Array.Empty<string>();
    }
}
