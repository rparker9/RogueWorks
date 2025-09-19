// File: Assets/RogueWorks.Unity/Presentation/ActorViewRegistry.cs
// Purpose: POCO replacement for ActorViewRegistryMono.
using System;
using System.Collections.Generic;

namespace RogueWorks.Unity.Presentation
{
    public interface IActorViewLookup
    {
        bool TryGet(int actorId, out ActorView view);
    }

    [Serializable]
    public sealed class ActorViewRegistry : IActorViewLookup
    {
        private readonly Dictionary<int, RogueWorks.Unity.Presentation.ActorView> _byId = new();

        public void Register(RogueWorks.Unity.Presentation.ActorView view)
        {
            if (view == null) return;
            _byId[view.ActorId] = view;
        }

        public bool TryGet(int actorId, out RogueWorks.Unity.Presentation.ActorView view) =>
            _byId.TryGetValue(actorId, out view);

        public void Unregister(int actorId) => _byId.Remove(actorId);

        public List<ActorView> All() => new(_byId.Values);
    }
}