using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace IFWilly4C;

public sealed class ModEntry : Mod
{
    private Texture2D? illustration;
private bool visible;
private float opacity;
private bool warnedInvalidEvent;
private const float FadeStep = 1f / 30f; // about 0.5 sec at 60 ticks/sec
private const string ImagePath = "assets/IFWilly4hrtCGFull.png";
private const string EventMarker = "쯧, 이런 건 위에부터 빼야지.";

    public override void Entry(IModHelper helper)
    {
        try
        {
            illustration = helper.ModContent.Load<Texture2D>(ImagePath);
            Monitor.Log($"4-heart CG image loaded: {illustration.Width}x{illustration.Height}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"4-heart CG image load failed: {ex}", LogLevel.Error);
        }

        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Display.RenderedWorld += OnRenderedWorld;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
            visible = false;
            opacity = 0f;
        };
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        try
        {
            object? ev = GetStaticMember("StardewValley.Game1, Stardew Valley", "CurrentEvent")
                ?? GetStaticMember("StardewValley.Game1, Stardew Valley", "currentEvent");

            if (ev is null)
            {
                SetVisible(false);
                warnedInvalidEvent = false;
            }
            else
            {
                string[] commands = GetCommands(ev);
                if (commands.Length == 0 || !commands.Any(c => c.Contains(EventMarker, StringComparison.Ordinal)))
                {
                    SetVisible(false);
                }
                else
                {
                    int showAt = Array.FindIndex(commands, c => c.Trim().Equals("pause 003", StringComparison.Ordinal));
                    int hideAt = Array.FindIndex(commands, c => c.Trim().Equals("pause 004", StringComparison.Ordinal));
                    int? current = GetIntMember(ev, "CurrentCommand", "currentCommand", "currentCommandIndex", "CurrentCommandIndex");

                    if (showAt < 0 || hideAt <= showAt || current is null)
                    {
                        if (!warnedInvalidEvent)
                        {
                            Monitor.Log($"4-heart CG commands unavailable: show={showAt}, hide={hideAt}, current={(current?.ToString() ?? "null")}", LogLevel.Warn);
                            warnedInvalidEvent = true;
                        }
                        SetVisible(false);
                    }
                    else
                    {
                        warnedInvalidEvent = false;
                        SetVisible(current.Value >= showAt && current.Value < hideAt);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SetVisible(false);
            Monitor.Log($"4-heart CG event detection failed: {ex}", LogLevel.Error);
        }

        // Keep rendering as opacity decreases even after the hide marker.
        opacity = MathHelper.Clamp(opacity + (visible ? FadeStep : -FadeStep), 0f, 1f);
    }

    private void SetVisible(bool value)
    {
        if (visible == value)
            return;
        visible = value;
        Monitor.Log(value ? "4-heart CG SHOW" : "4-heart CG HIDE", LogLevel.Info);
    }

    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (opacity <= 0f || illustration is null)
            return;

        Rectangle screen = e.SpriteBatch.GraphicsDevice.Viewport.Bounds;
        if (screen.Width < 1 || screen.Height < 1)
            return;

        float scale = Math.Max(screen.Width / (float)illustration.Width, screen.Height / (float)illustration.Height);
        int width = Math.Max(1, (int)MathF.Ceiling(illustration.Width * scale));
        int height = Math.Max(1, (int)MathF.Ceiling(illustration.Height * scale));
        int x = screen.X + (screen.Width - width) / 2;
        int y = screen.Y + (screen.Height - height) / 2;
        e.SpriteBatch.Draw(illustration, new Rectangle(x, y, width, height), Color.White * opacity);
    }

    private static object? GetStaticMember(string typeName, string name)
    {
        Type? type = Type.GetType(typeName);
        if (type is null) return null;
        return GetMember(type, null, name);
    }

    private static object? GetMember(Type type, object? target, string name)
    {
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | (target is null ? BindingFlags.Static : BindingFlags.Instance);
        return type.GetProperty(name, flags)?.GetValue(target) ?? type.GetField(name, flags)?.GetValue(target);
    }

    private static object? GetMember(object target, string name) => GetMember(target.GetType(), target, name);

    private static int? GetIntMember(object target, params string[] names)
    {
        foreach (string name in names)
            if (GetMember(target, name) is int value) return value;
        return null;
    }

    private static string[] GetCommands(object target)
    {
        object? raw = GetMember(target, "eventCommands") ?? GetMember(target, "EventCommands");
        if (raw is string[] array) return array;
        if (raw is IEnumerable enumerable)
        {
            var lines = new List<string>();
            foreach (object? obj in enumerable)
                if (obj is string text) lines.Add(text);
            return lines.ToArray();
        }
        return Array.Empty<string>();
    }
}

