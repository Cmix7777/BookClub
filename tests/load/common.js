// Общие кирпичики для нагрузочных сценариев BookClub (k6).
//
// Сценарии ходят в сайт так же, как обычный посетитель: по HTTP. Код сайта они не знают.
// Всё, что умеет делать «виртуальный пользователь» (VU), собрано здесь, а smoke.js и load.js
// только решают, сколько таких пользователей запустить и как быстро.

import http from 'k6/http';
import { check } from 'k6';

export const BASE = (__ENV.BOOKCLUB_URL || 'http://127.0.0.1:5153').replace(/\/$/, '');
export const PASSWORD = 'Passw0rd!test';

// Уникальная метка: номер виртуального пользователя, номер его круга и случайный хвост.
// Логин сайта допускает латиницу, цифры, точку и подчёркивание и не длиннее 30 знаков.
// В подготовительном шаге setup() номера __ITER у k6 нет, поэтому подстраховываемся.
function unique(prefix) {
  const vu = typeof __VU === 'undefined' ? 0 : __VU;
  const iter = typeof __ITER === 'undefined' ? 0 : __ITER;
  return prefix + vu + 'x' + iter + 'x' + Math.random().toString(36).slice(2, 8);
}

// Параметры запроса. Тег name нужен, чтобы k6 считал время отдельно по каждому ВИДУ запроса
// (регистрация, вход, голос...), а не по каждому адресу: в адресах бывают случайные номера.
function params(name, token, expected) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers.Authorization = 'Bearer ' + token;
  const result = { headers, tags: { name } };
  // По умолчанию k6 считает ошибкой любой ответ не из диапазона 200-399.
  // Повторный голос сайт обязан отклонить кодом 409, поэтому такой ответ мы объявляем ожидаемым.
  if (expected) result.responseCallback = http.expectedStatuses(expected);
  return result;
}

export function get(name, path, token) {
  return http.get(BASE + '/' + path, params(name, token));
}

export function post(name, path, body, token, expected) {
  const payload = body === null || body === undefined ? null : JSON.stringify(body);
  return http.post(BASE + '/' + path, payload, params(name, token, expected));
}

// Достаёт поле из JSON-ответа и не падает, если в ответе не JSON.
export function field(response, selector) {
  try {
    return response.json(selector);
  } catch (e) {
    return undefined;
  }
}

// Новый пользователь: регистрация и вход. Возвращает токен или null, если что-то пошло не так.
export function newUser() {
  const name = unique('lt');
  const registered = post('register', 'api/auth/register', {
    username: name,
    email: name + '@example.test',
    password: PASSWORD,
  });
  check(registered, { 'регистрация: 201': (r) => r.status === 201 });

  const login = post('login', 'api/auth/login', { login: name, password: PASSWORD });
  const token = field(login, 'token');
  check(login, { 'вход: 200 и выдан токен': (r) => r.status === 200 && !!token });
  return token || null;
}

// Подготовка перед нагрузкой: владелец, клуб, каталог книг и один идущий отбор из двух книг.
// extraBooks добавляет в каталог «лишние» книги, чтобы список книг был не пустым, как на настоящем сайте.
export function seedClub(extraBooks) {
  const ownerToken = newUser();
  const club = post(
    'seed_club',
    'api/clubs',
    { name: 'Нагрузка ' + unique('c'), description: 'Клуб для нагрузочной проверки' },
    ownerToken,
  );
  const clubId = field(club, 'id');

  const bookIds = [];
  for (let i = 0; i < 2 + extraBooks; i++) {
    const book = post(
      'seed_book',
      'api/books',
      {
        title: 'Нагрузочная книга ' + i + ' ' + unique('b'),
        author: 'Автор ' + (i % 20),
        genre: 'Нагрузка',
        year: 1990 + (i % 30),
        pages: 100 + i,
      },
      ownerToken,
    );
    bookIds.push(field(book, 'id'));
  }

  const poll = post(
    'seed_poll',
    'api/polls',
    {
      clubId,
      question: 'Что читаем в нагрузочном тесте?',
      bookIds: bookIds.slice(0, 2),
      durationMinutes: 120,
    },
    ownerToken,
  );
  const options = (field(poll, 'options') || []).map((o) => o.id);
  const data = { ownerToken, clubId, pollId: field(poll, 'id'), options };

  if (!ownerToken || !clubId || !data.pollId || options.length !== 2) {
    throw new Error('Не удалось подготовить данные для нагрузки: отбор ответил ' + poll.status + ' ' + poll.body);
  }
  return data;
}

// Читатель: открывает главную страницу сайта, то есть список клубов и каталог книг. Вход не нужен.
export function browse() {
  const clubs = get('list_clubs', 'api/clubs');
  check(clubs, { 'список клубов: 200': (r) => r.status === 200 });

  const books = get('list_books', 'api/books');
  check(books, { 'каталог книг: 200': (r) => r.status === 200 });
}

// Голосующий: регистрируется, вступает в клуб, смотрит отбор, голосует, пробует проголосовать второй раз
// и заглядывает в результаты. Пока отбор идёт, результаты должны оставаться «запечатанными».
export function voterJourney(data) {
  const token = newUser();
  if (!token) return;

  const join = post('join_club', 'api/clubs/' + data.clubId + '/join', null, token);
  check(join, { 'вступление в клуб: 200': (r) => r.status === 200 });

  const poll = get('get_poll', 'api/polls/' + data.pollId, token);
  check(poll, {
    'отбор открывается: 200 и два варианта': (r) => r.status === 200 && (field(r, 'options') || []).length === 2,
  });

  const choice = data.options[Math.floor(Math.random() * data.options.length)];
  const vote = post('vote', 'api/polls/' + data.pollId + '/vote', { pollOptionId: choice }, token);
  check(vote, { 'голос принят: 200': (r) => r.status === 200 });

  const again = post('vote_again', 'api/polls/' + data.pollId + '/vote', { pollOptionId: choice }, token, 409);
  check(again, { 'повторный голос отклонён: 409': (r) => r.status === 409 });

  const results = get('results', 'api/polls/' + data.pollId + '/results', token);
  check(results, {
    'результаты: 200, конверт запечатан': (r) => {
      if (r.status !== 200) return false;
      const total = field(r, 'totalVotes');
      return total === null || total === undefined;
    },
  });
}
