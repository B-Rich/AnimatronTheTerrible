﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using SnipSnap.Mathematics;
using TwistedOak.Collections;
using TwistedOak.Element.Env;
using TwistedOak.Util;
using System.Linq;

namespace Animatron {
    public sealed class Animation {
        public readonly PerishableCollection<Action<Step>> StepActions = new PerishableCollection<Action<Step>>(); 
        public readonly PerishableCollection<UIElement> Controls = new PerishableCollection<UIElement>();
        public readonly PerishableCollection<PointDesc> Points = new PerishableCollection<PointDesc>();
        public readonly PerishableCollection<RectDesc> Rects = new PerishableCollection<RectDesc>();
        public readonly PerishableCollection<PolygonDesc> Polygons = new PerishableCollection<PolygonDesc>();
        public readonly PerishableCollection<LineSegmentDesc> Lines = new PerishableCollection<LineSegmentDesc>();
        public readonly PerishableCollection<TextDesc> Labels = new PerishableCollection<TextDesc>();

        public void LinkMany(IAni<IEnumerable<PointDesc>> values, Lifetime life) {
            LinkMany(values, Points, life);
        }
        public void LinkMany(IAni<IEnumerable<RectDesc>> values, Lifetime life) {
            LinkMany(values, Rects, life);
        }
        public void LinkMany(IAni<IEnumerable<PolygonDesc>> values, Lifetime life) {
            LinkMany(values, Polygons, life);
        }
        public void LinkMany(IAni<IEnumerable<LineSegmentDesc>> values, Lifetime life) {
            LinkMany(values, Lines, life);
        }
        public void LinkMany(IAni<IEnumerable<TextDesc>> values, Lifetime life) {
            LinkMany(values, Labels, life);
        }
        private void LinkMany<T>(IAni<IEnumerable<T>> values, PerishableCollection<T> col, Lifetime life) {
            var d = new Dictionary<T, LifetimeSource>();
            NextElapsedTime().Subscribe(
                t => {
                    var cur = values.ValueAt(t).Distinct();
                    var stale = d.Keys.Except(cur);
                    var fresh = cur.Except(d.Keys);
                    foreach (var e in fresh) {
                        d.Add(e, life.CreateDependentSource());
                        col.Add(e, d[e].Lifetime);
                    }
                    foreach (var e in stale) {
                        d[e].EndLifetime();
                        d.Remove(e);
                    }
                },
                life);
        }

        public IObservable<TimeSpan> NextElapsedTime() {
            return Dynamic(step => step.NextTotalElapsedTime);
        }
        public IObservable<T> Dynamic<T>(Func<Step, T> stepper) {
            return new AnonymousObservable<T>(observer => {
                var life = new DisposableLifetime();
                StepActions.Add(
                    step => observer.OnNext(stepper(step)),
                    life.Lifetime);
                return life;
            });
        }
        public IObservable<T> Dynamic<T>(T seed, Func<T, Step, T> stepper) {
            return new AnonymousObservable<T>(observer => {
                var cur = seed;
                return Dynamic(step => cur = stepper(cur, step)).Subscribe(observer);
            });
        }
        public Animation() {
            Points.CurrentAndFutureItems().Subscribe(e => {
                var r = new Ellipse();
                e.Value.Link(r, NextElapsedTime(), e.Lifetime);
                Controls.Add(r, e.Lifetime);
            });
            Rects.CurrentAndFutureItems().Subscribe(e => {
                var r = new Rectangle();
                e.Value.Link(r, NextElapsedTime(), e.Lifetime);
                Controls.Add(r, e.Lifetime);
            });
            Lines.CurrentAndFutureItems().Subscribe(e => {
                var r = new Line();
                e.Value.Link(r, NextElapsedTime(), e.Lifetime);
                Controls.Add(r, e.Lifetime);
            });
            Labels.CurrentAndFutureItems().Subscribe(e => {
                var r = new TextBlock();
                e.Value.Link(r, NextElapsedTime(), e.Lifetime);
                Controls.Add(r, e.Lifetime);
            });
            Polygons.CurrentAndFutureItems().Subscribe(e => {
                var r = new Polygon();
                e.Value.Link(r, NextElapsedTime(), e.Lifetime);
                Controls.Add(r, e.Lifetime);
            });
        }
        public async Task Run(Lifetime life, TimeSpan? delayTime = default(TimeSpan?)) {
            var clock = new Stopwatch();
            clock.Start();

            var lastTime = clock.Elapsed;
            var lostTime = 0.Seconds();
            while (!life.IsDead) {
                var dt = clock.Elapsed - lastTime;
                var cdt = dt.Clamp(TimeSpan.Zero, TimeSpan.FromSeconds(1));
                lostTime += dt - cdt;

                var step = new Step(
                    previousTotalElapsedTime: lastTime, 
                    timeStep: cdt);
                lastTime += cdt;

                foreach (var e in StepActions.CurrentItems())
                    e.Value.Invoke(step);

                await Task.Delay(delayTime ?? 30.Milliseconds());
            }
        }
        public void LinkToCanvas(Canvas canvas, Lifetime life) {
            Controls.CurrentAndFutureItems().Subscribe(
                e => {
                    canvas.Children.Add(e.Value);
                    e.Lifetime.WhenDead(() => canvas.Children.Remove(e.Value));
                },
                life);
        }
    }
}