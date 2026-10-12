// Нагрузка на BookClub: две группы посетителей работают одновременно.
//
// readers: читатели, которые листают клубы и каталог. Их число растёт до 15 и держится.
// voters: новые люди, которые регистрируются и голосуют в одном и том же отборе. Новых людей приходит
//         по 3 в секунду. Это самая тяжёлая часть: регистрация и вход считают хэш пароля (BCrypt),
//         а он намеренно медленный и грузит процессор.
//
// Пороги ниже (thresholds) это «планка приёмки». Если любая не выполнена, k6 завершается с ошибкой
// и запуск в GitHub Actions становится красным. Планка выставлена по первому замеру на раннере GitHub:
// чтение отвечало за 2-5 мс, голос за 5-80 мс (зависит от того, сколько хэшей паролей считается в этот момент),
// регистрация и вход за 130-180 мс. Планка выше этих цифр минимум в 3 раза, чтобы шум раннера не краснил запуск,
// но настоящее замедление было заметно.

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
    dropped_iterations: ['count==0'],    // сервер успевает принимать всех новых людей
    // чтение на раннере занимает единицы миллисекунд, планка в 100 мс даёт большой запас на шум
    'http_req_duration{name:list_clubs}': ['p(95)<100'],
    'http_req_duration{name:list_books}': ['p(95)<100'],
    // голос сам лёгкий (5 мс), но если рядом считаются хэши паролей, он ждёт очереди к процессору: во втором замере 77 мс
    'http_req_duration{name:vote}': ['p(95)<250'],
    // регистрация и вход считают хэш пароля (около 150 мс), поэтому планка выше
    'http_req_duration{name:register}': ['p(95)<600'],
    'http_req_duration{name:login}': ['p(95)<600'],
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
