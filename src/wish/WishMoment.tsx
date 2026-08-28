import { useEffect, useState } from 'react';
import type { WishMomentProjection } from './types';
import './wish.css';

export type WishMomentProps = {
  projection: WishMomentProjection;
  onComplete: (seconds: number) => Promise<void>;
};

export function WishMoment({ projection, onComplete }: WishMomentProps) {
  const [seconds, setSeconds] = useState(0);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const timer = setInterval(() => {
      setSeconds((current) => (current >= 60 ? 60 : current + 1));
    }, 1000);
    return () => clearInterval(timer);
  }, []);

  async function complete(): Promise<void> {
    setSaving(true);
    setError(null);
    try {
      await onComplete(seconds);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="wish-moment" aria-label="願いタイム">
      <p className="eyebrow">WISH MOMENT</p>
      <h3>30〜60秒、選ばれた願いに静かに向き合う。</h3>
      <p className="wish-moment-timer">{seconds}s / 30s minimum</p>
      <div className="wish-moment-cards">
        {projection.wishes.map((wish) => (
          <article key={wish.wishId}><strong>{wish.text}</strong><span>締切 {wish.deadline}</span></article>
        ))}
      </div>
      <p className="sealed-note">ここへ渡されるのは実践群だけです。封印群の本文はdomain projectionの時点で除外されています。</p>
      <button type="button" className="wish-register-button" disabled={seconds < 30 || saving} onClick={() => void complete()}>
        {saving ? '記録中…' : seconds < 30 ? `あと${30 - seconds}秒` : '願いタイムを完了'}
      </button>
      {error && <p className="error-text" role="alert">{error}</p>}
    </section>
  );
}
