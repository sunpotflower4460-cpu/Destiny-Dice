import { useEffect, useState } from 'react';
import { initDatabase } from './db/sqlite';
import './App.css';

type DbStatus = 'checking' | 'ok' | 'ng';

function App() {
  const [dbStatus, setDbStatus] = useState<DbStatus>('checking');
  const [dbError, setDbError] = useState<string | undefined>(undefined);

  useEffect(() => {
    let cancelled = false;

    initDatabase().then((result) => {
      if (cancelled) return;
      if (result.ok) {
        setDbStatus('ok');
      } else {
        setDbStatus('ng');
        setDbError(result.error);
      }
    });

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <main className="placeholder">
      <h1>Intention Dice</h1>
      <p className="message">実験は準備中</p>
      <p className={`db-status db-status--${dbStatus}`}>
        DB接続: {dbStatus === 'checking' ? '確認中…' : dbStatus === 'ok' ? 'OK' : 'NG'}
      </p>
      {dbStatus === 'ng' && dbError && <p className="db-error">{dbError}</p>}
    </main>
  );
}

export default App;
