// Нагрузка на BookClub: две группы посетителей работают одновременно.
//
// readers: читатели, которые листают клубы и каталог. Их число растёт до 15 и держится.
// voters: новые люди, которые регистрируются и голосуют в одном и том же отборе. Новых людей приходит
//         по 3 в секунду. Это самая тяжёлая часть: регистрация и вход считают хэш пароля (BCrypt),
//         а он намеренно медленный и грузит процессор.
//
// Пороги ниже (thresholds) это «планка приёмки». Если любая не выполнена, k6 завершается с ошибкой
// и запуск в GitHub Actions становится красным. Первые значения заведомо щедрые: по результатам
// первого замера на раннере их подтягивают так, чтобы планка была чуть выше обычных цифр.

import { sleep } from 'k6';
import { seedClub, voterJourney, browse } from './common.js';

export const options = {
  scenarios: {
    readers: {
      executor: 'ramping-vus',
      exec: 'readers',
      startVUs: 0,
      stages: [
        { duration: '20s', target: 15 }, // разгон
        { duration: '40s', target: 15 }, // держим
        { duration: '10s', target: 0 },  // спад
      ],
      gracefulRampDown: '5s',
    },
    voters: {
      executor: 'ramping-arrival-rate',
      exec: 'voters',
      startRate: 1,
      timeUnit: '1s',
      preAllocatedVUs: 20,
      maxVUs: 80,
      stages: [
        { target: 3, duration: '20s' },
        { target: 3, duration: '40s' },
        { target: 0, duration: '10s' },
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],      // не больше 1% неожиданных ответов
    checks: ['rate>0.99'],               // не меньше 99% проверок внутри сценария
    dropped_iterations: ['count<5'],     // сервер успевает принимать новых людей
    'http_req_duration{name:list_clubs}': ['p(95)<1500'],
    'http_req_duration{name:list_books}': ['p(95)<1500'],
    'http_req_duration{name:vote}': ['p(95)<2000'],
    'http_req_duration{name:register}': ['p(95)<4000'],
    'http_req_duration{name:login}': ['p(95)<4000'],
  },
};

// Выполняется один раз до нагрузки: клуб, 300 книг в каталоге и отбор, в котором все будут голосовать.
export function setup() {
  return seedClub(300);
}

export function readers() {
  browse();
  sleep(1); // «время на подумать»: человек не обновляет страницу тысячу раз в секунду
}

export function voters(data) {
  voterJourney(data);
}
