// Дымовая проверка: один виртуальный пользователь проходит весь путь один раз.
//
// Цель не нагрузить, а убедиться, что сам СЦЕНАРИЙ рабочий: адреса верные, ответы такие, как мы ждём.
// Если дым не прошёл, нагрузку запускать бессмысленно: она бы просто мерила скорость ошибок.

import { seedClub, voterJourney, browse } from './common.js';

export const options = {
  vus: 1,
  iterations: 1,
  thresholds: {
    http_req_failed: ['rate==0'], // ни одного неожиданного кода ответа
    checks: ['rate==1'],          // все проверки внутри сценария выполнены
  },
};

export function setup() {
  return seedClub(0);
}

export default function (data) {
  browse();
  voterJourney(data);
}
