using System.Threading.Tasks;
using MindAttic.Chain.Core;
using Unity.Multiplayer; // IBotDriver, NetBot, [NetBotDriver]
using UnityEngine;
using UnityEngine.Scripting;

namespace MindAttic.Chain.Online
{
    /// <summary>
    /// The game's bot (U.CHN-T26): launched with <c>-bot A</c> (NetBotTest, <c>mp-host game smoke</c>), the kit joins by
    /// code with no menus and hands the connected session here. It plays like a player does, with the same commands its
    /// input readers would make, sent through its <see cref="ChainPlayer"/>: it waits for its seat, steers its angel
    /// right (role A) or left (any other role), swings the grapple, and passes once the authority has moved its angel.
    /// </summary>
    [NetBotDriver, Preserve]
    public sealed class ChainBot : IBotDriver
    {
        /// <summary>World units the authority must move the bot's angel before it passes.</summary>
        public const float PassDistance = 0.5f;

        /// <summary>Mouse counts a steer asks for: a brisk sideways sweep.</summary>
        public const float SteerCounts = 40f;

        public static float DirectionFor(string role) => role == "A" ? 1f : -1f;

        public async Task RunAsync(NetBot bot)
        {
            var me = await bot.LocalPlayer<ChainPlayer>();
            // The server seats it and writes its angel in the same spawn, so the start arrives with the seat.
            await bot.Until(() => me.IsSeated, "the server to seat the bot");
            var start = me.Angel;
            var direction = DirectionFor(bot.Role);

            me.SendCommand(new ArmGrapple(me.PlayerId));
            while (Mathf.Abs(me.Angel.x - start.x) < PassDistance)
            {
                // The player id in a command is ignored: the server plays it as the bot's own seat.
                me.SendCommand(new SteerAngel(me.PlayerId, new Vec2(SteerCounts * direction, 0f), 0.05f));
                await bot.Seconds(0.05f);
                if (bot.SecondsLeft <= 0f)
                {
                    break;
                }
            }

            me.SendCommand(new ReleaseGrapple(me.PlayerId));
            if (Mathf.Abs(me.Angel.x - start.x) < PassDistance)
            {
                bot.Fail("the authority never moved seat " + me.Seat + "'s angel from " + start + " (now " + me.Angel + ")");
                return;
            }

            bot.Pass("seat " + me.Seat + " flew from " + start + " to " + me.Angel + " as " + me.DisplayName);
        }
    }
}
