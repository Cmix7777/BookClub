// Поиск предела: сколько новых людей в секунду выдерживает сайт на этом раннере.
//
// Идея простая: пять ступенек подряд, на каждой скорость прихода новых людей выше. На каждой ступеньке
// k6 печатает, как менялось время регистрации. Пока сервер справляется, время почти не меняется. Как только
// запросы начинают копиться в очереди, время резко растёт, и это и есть предел.
//
// Ожидание заранее (его потом сверяем с измерением): регистрация и вход считают хэш пароля около 150 мс
// процессорного времени каждый, то есть около 300 мс на человека. Раннер имеет 4 ядра, значит, потолок
// где-то около 13 новых людей в секунду.
//
// Это измерение, а не приёмка, поэтому пороги ниже заведомо не нарушаются: они нужны лишь затем,
// чтобы k6 вывел цифры по каждой ступеньке отдельно.

import { seedClub, voterJourney } from './common.js';

const STEPS = [4, 8, 12, 16, 20]; // новых людей в секунду на ступеньке
const LENGTH = 15;                // секунд на ступеньку

function step(rate, index) {
  return {
    executor: 'constant-arrival-rate',
    exec: 'voters',
    rate,
    timeUnit: '1s',
    duration: LENGTH + 's',
    startTime: index * (LENGTH + 2) + 's',
    preAllocatedVUs: 40,
    maxVUs: 400,
    gracefulStop: '10s',
  };
}

export const options = {
  scenarios: {
    rate_04: step(STEPS[0], 0),
    rate_08: step(STEPS[1], 1),
    rate_12: step(STEPS[2], 2),
    rate_16: step(STEPS[3], 3),
    rate_20: step(STEPS[4], 4),
  },
  thresholds: {
    'http_req_duration{scenario:rate_04,name:register}': ['p(95)<1000000'],
    'http_req_duration{scenario:rate_08,name:register}': ['p(95)<1000000'],
    'http_req_duration{scenario:rate_12,name:register}': ['p(95)<1000000'],
    'http_req_duration{scenario:rate_16,name:register}': ['p(95)<1000000'],
    'http_req_duration{scenario:rate_20,name:register}': ['p(95)<1000000'],
    'iterations{scenario:rate_04}': ['count>=0'],
    'iterations{scenario:rate_08}': ['count>=0'],
    'iterations{scenario:rate_12}': ['count>=0'],
    'iterations{scenario:rate_16}': ['count>=0'],
    'iterations{scenario:rate_20}': ['count>=0'],
  },
};

export function setup() {
  return seedClub(0);
}

export function voters(data) {
  voterJourney(data);
}
