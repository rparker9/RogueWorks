using RogueWorks.Core.Actions;
using RogueWorks.Core.Controllers;
using UnityEngine;

namespace RogueWorks.Core.AI
{
    public sealed class SimpleAI : IActorController
    {
        private readonly int _id;
        public SimpleAI(int id) { _id = id; }
        public ActionIntent? GetIntent() => null;
    }
}
