using System.Collections.Generic;
using AgeOfSakura.Game;
using NUnit.Framework;
using UnityEngine;

namespace AgeOfSakura.Tests
{
    /// <summary>Gesture conflict rules: taps vs drags vs long press vs pinch, and UI never leaking into the world.</summary>
    public class GestureRecognizerTests
    {
        private const float Threshold = 20f;
        private const float LongPress = 0.5f;

        private GestureRecognizer recognizer;
        private bool overUi;
        private float now;
        private readonly List<string> events = new List<string>();
        private readonly List<PointerSample> pointers = new List<PointerSample>();

        [SetUp]
        public void SetUp()
        {
            overUi = false;
            now = 0f;
            events.Clear();
            pointers.Clear();
            recognizer = new GestureRecognizer(Threshold, LongPress, p => overUi);
            recognizer.Tap += p => events.Add("tap");
            recognizer.LongPress += p => events.Add("long");
            recognizer.DragBegin += (start, cur, afterLong) => events.Add(afterLong ? "dragBeginAfterLong" : "dragBegin");
            recognizer.DragMove += (pos, delta) => events.Add("drag");
            recognizer.DragEnd += (pos, cancelled) => events.Add(cancelled ? "dragEndCancelled" : "dragEnd");
            recognizer.Pinch += (scale, center) => events.Add("pinch");
        }

        private void Frame(float dt, params (int id, float x, float y)[] touches)
        {
            now += dt;
            pointers.Clear();
            foreach (var t in touches) pointers.Add(new PointerSample { Id = t.id, Position = new Vector2(t.x, t.y) });
            recognizer.Update(pointers, now);
        }

        [Test]
        public void QuickPressRelease_IsATap()
        {
            Frame(0.016f, (1, 100, 100));
            Frame(0.05f);
            CollectionAssert.AreEqual(new[] { "tap" }, events);
        }

        [Test]
        public void PressBegan_FiresOnTouchDown_BeforeTheGestureIsKnown_ButNotOverUi()
        {
            recognizer.PressBegan += p => events.Add("press");
            Frame(0.016f, (1, 100, 100));
            CollectionAssert.AreEqual(new[] { "press" }, events);

            Frame(0.05f);
            events.Clear();
            overUi = true;
            Frame(0.016f, (1, 100, 100));
            Frame(0.05f);
            CollectionAssert.IsEmpty(events, "a press on UI never reaches the world");
        }

        [Test]
        public void SmallMovement_StaysATap()
        {
            Frame(0.016f, (1, 100, 100));
            Frame(0.05f, (1, 108, 104));
            Frame(0.05f);
            CollectionAssert.AreEqual(new[] { "tap" }, events);
        }

        [Test]
        public void MovementBeyondThreshold_BecomesDrag_NoTap()
        {
            Frame(0.016f, (1, 100, 100));
            Frame(0.016f, (1, 140, 100));
            Frame(0.016f, (1, 180, 100));
            Frame(0.016f);
            CollectionAssert.AreEqual(new[] { "dragBegin", "drag", "dragEnd" }, events);
        }

        [Test]
        public void HoldWithoutMoving_FiresLongPress_ThenNoTapOnRelease()
        {
            Frame(0.016f, (1, 100, 100));
            Frame(0.6f, (1, 100, 100));
            Frame(0.05f);
            CollectionAssert.AreEqual(new[] { "long" }, events);
        }

        [Test]
        public void DragAfterLongPress_ContinuesAsDrag_FlaggedAsAfterLongPress()
        {
            Frame(0.016f, (1, 100, 100));
            Frame(0.6f, (1, 100, 100));
            Frame(0.016f, (1, 160, 100));
            Frame(0.016f, (1, 200, 100));
            Frame(0.016f);
            CollectionAssert.AreEqual(new[] { "long", "dragBeginAfterLong", "drag", "dragEnd" }, events);
        }

        [Test]
        public void PressStartingOnUi_NeverProducesWorldGestures()
        {
            overUi = true;
            Frame(0.016f, (1, 100, 100));
            overUi = false; // the finger then slides off the button
            Frame(0.016f, (1, 300, 100));
            Frame(0.7f, (1, 300, 100));
            Frame(0.016f);
            Assert.IsEmpty(events);
        }

        [Test]
        public void TwoFingers_Pinch_AndNeverSelectOrTap()
        {
            Frame(0.016f, (1, 100, 100));
            Frame(0.016f, (1, 100, 100), (2, 200, 100));
            Frame(0.016f, (1, 90, 100), (2, 210, 100));
            Frame(0.016f, (1, 80, 100), (2, 220, 100));
            Frame(0.016f, (2, 220, 100)); // one finger lifted
            Frame(0.016f);                // all lifted
            CollectionAssert.DoesNotContain(events, "tap");
            Assert.AreEqual(2, events.FindAll(e => e == "pinch").Count);
        }

        [Test]
        public void SecondFinger_CancelsRunningDrag()
        {
            Frame(0.016f, (1, 100, 100));
            Frame(0.016f, (1, 160, 100));
            Frame(0.016f, (1, 200, 100), (2, 400, 100));
            CollectionAssert.Contains(events, "dragEndCancelled");
        }

        [Test]
        public void FingerLeftAfterPinch_IsIgnoredUntilLifted()
        {
            Frame(0.016f, (1, 100, 100), (2, 200, 100));
            Frame(0.016f, (2, 200, 100));
            Frame(0.016f, (2, 400, 300));
            Frame(0.016f);
            CollectionAssert.DoesNotContain(events, "dragBegin");
            CollectionAssert.DoesNotContain(events, "tap");
        }

        [Test]
        public void SecondFingerOnUi_DoesNotStartPinch()
        {
            Frame(0.016f, (1, 100, 100));
            overUi = true;
            Frame(0.016f, (1, 100, 100), (2, 200, 100));
            Frame(0.016f, (1, 90, 100), (2, 220, 100));
            CollectionAssert.DoesNotContain(events, "pinch");
        }

        [Test]
        public void NewGestureWorksAfterRelease()
        {
            Frame(0.016f, (1, 100, 100));
            Frame(0.05f);
            Frame(0.016f, (3, 50, 50));
            Frame(0.05f);
            Assert.AreEqual(2, events.FindAll(e => e == "tap").Count);
        }
    }
}
