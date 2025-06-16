# 🚀 Пошаговая инструкция: Эффективный массовый парсинг документации

## 📋 Подготовка к парсингу

### 1. Инициализация проекта в памяти
```javascript
// Создаём проект для хранения данных
const projectId = "VSExtensibility";
```

### 2. Настройка браузера для максимальной производительности
```javascript
// Оптимальные настройки для Playwright
const browserOptions = {
  headless: true,           // Ускорение в 2-3 раза
  timeout: 10000,          // 10 секунд - баланс скорости и надёжности
  browserType: 'chromium'  // Наиболее стабильный
};
```

## 🎯 Этап 1: Автоматический сбор всех ссылок

### Открытие стартовой страницы
```javascript
await playwright.navigate("https://learn.microsoft.com/ru-ru/visualstudio/extensibility/visualstudio.extensibility?view=vs-2022");
```

### Извлечение уникальных ссылок одним скриптом
```javascript
const allLinks = Array.from(document.querySelectorAll('a[href*="visualstudio.extensibility"]'))
  .map(link => link.href)
  .filter(href => href.includes('learn.microsoft.com'))
  .filter(href => !href.includes('#'))  // Убираем якоря
  .filter((href, index, array) => array.indexOf(href) === index); // Уникальные
```

### Сохранение реестра ссылок
```javascript
// Сохраняем полный список для отслеживания прогресса
SaveProjectArtifact({
  title: "Реестр ссылок для парсинга",
  type: "Reference",
  content: JSON.stringify(allLinks, null, 2),
  projectId: "VSExtensibility"
});
```

## ⚡ Этап 2: Оптимизированный парсинг по циклу

### Стратегия обработки
```javascript
const processPage = async (url, index, total) => {
  try {
    console.log(`[${index+1}/${total}] Обрабатываем: ${url}`);
    
    // Проверяем, не обрабатывали ли уже эту страницу
    const existing = await SearchProjectArtifacts({
      query: url,
      projectId: "VSExtensibility"
    });
    
    if (existing.length > 0) {
      console.log("✅ Страница уже обработана, пропускаем");
      return;
    }
    
    // Переходим на страницу
    await playwright.navigate(url);
    
    // Извлекаем весь видимый текст
    const content = await playwright.getVisibleText();
    
    // Извлекаем заголовок
    const title = await playwright.evaluate(`
      document.querySelector('h1')?.textContent || 
      document.title.split(' | ')[0]
    `);
    
    // Структурируем и сохраняем
    await SaveProjectArtifact({
      title: title,
      type: "Document",
      content: `# ${title}\n\n**Источник:** ${url}\n\n${content}`,
      projectId: "VSExtensibility",
      tags: "visual-studio,extensibility,documentation,auto-parsed"
    });
    
    console.log(`✅ Сохранено: ${title}`);
    
  } catch (error) {
    console.error(`❌ Ошибка при обработке ${url}:`, error);
  }
};
```

### Последовательная обработка с контролем скорости
```javascript
for (let i = 0; i < allLinks.length; i++) {
  await processPage(allLinks[i], i, allLinks.length);
  
  // Небольшая пауза для стабильности
  await new Promise(resolve => setTimeout(resolve, 1000));
  
  // Каждые 5 страниц - проверяем статистику
  if ((i + 1) % 5 === 0) {
    const stats = await GetMemoryStatistics({projectId: "VSExtensibility"});
    console.log(`📊 Обработано: ${i+1}/${allLinks.length}, в базе: ${stats.totalArtifacts} артефактов`);
  }
}
```

## 🎛️ Этап 3: Мониторинг и контроль качества

### Проверка полноты парсинга
```javascript
// Получаем список всех сохранённых артефактов
const savedArtifacts = await GetProjectArtifacts({
  projectId: "VSExtensibility",
  groupByType: true
});

// Подсчитываем обработанные документы
const processedDocs = savedArtifacts.Document?.length || 0;
console.log(`📈 Обработано документов: ${processedDocs}/${allLinks.length}`);
```

### Поиск пропущенных страниц
```javascript
const findMissing = async () => {
  const processed = [];
  const allSaved = await GetProjectArtifacts({projectId: "VSExtensibility"});
  
  allSaved.forEach(artifact => {
    if (artifact.content.includes('**Источник:**')) {
      const urlMatch = artifact.content.match(/\*\*Источник:\*\* (https?:\/\/[^\n]+)/);
      if (urlMatch) processed.push(urlMatch[1]);
    }
  });
  
  const missing = allLinks.filter(link => !processed.includes(link));
  return missing;
};
```

## 🚀 Этап 4: Продвинутые техники оптимизации

### Пакетная обработка (для больших объёмов)
```javascript
const batchSize = 3;
const batches = [];

for (let i = 0; i < allLinks.length; i += batchSize) {
  batches.push(allLinks.slice(i, i + batchSize));
}

for (const batch of batches) {
  console.log(`🔄 Обрабатываем пакет из ${batch.length} страниц`);
  
  // Обрабатываем пакет последовательно
  for (const url of batch) {
    await processPage(url);
  }
  
  // Пауза между пакетами
  await new Promise(resolve => setTimeout(resolve, 2000));
}
```

### Умная фильтрация контента
```javascript
const cleanContent = (rawContent) => {
  return rawContent
    .replace(/\n{3,}/g, '\n\n')  // Убираем лишние переносы
    .replace(/\s+/g, ' ')       // Нормализуем пробелы
    .replace(/^\s+|\s+$/gm, '') // Убираем отступы в строках
    .slice(0, 8000);            // Ограничиваем размер для экономии места
};
```

## 📊 Этап 5: Анализ результатов

### Генерация отчёта о парсинге
```javascript
const generateReport = async () => {
  const stats = await GetMemoryStatistics({projectId: "VSExtensibility"});
  const artifacts = await GetProjectArtifacts({projectId: "VSExtensibility"});
  
  const report = {
    totalProcessed: artifacts.length,
    documentCount: artifacts.filter(a => a.type === 'Document').length,
    referenceCount: artifacts.filter(a => a.type === 'Reference').length,
    averageContentLength: artifacts.reduce((sum, a) => sum + a.content.length, 0) / artifacts.length,
    processingDate: new Date().toISOString()
  };
  
  await SaveProjectArtifact({
    title: "Отчёт о парсинге документации",
    type: "Reference",
    content: JSON.stringify(report, null, 2),
    projectId: "VSExtensibility"
  });
};
```

## ⚠️ Типичные ошибки и решения

### 1. Таймаут загрузки
```javascript
// Решение: увеличить timeout или добавить retry
const navigateWithRetry = async (url, maxRetries = 3) => {
  for (let i = 0; i < maxRetries; i++) {
    try {
      await playwright.navigate(url, {timeout: 15000});
      return;
    } catch (error) {
      if (i === maxRetries - 1) throw error;
      console.log(`⚠️ Повторная попытка ${i+1}/${maxRetries} для ${url}`);
      await new Promise(resolve => setTimeout(resolve, 2000));
    }
  }
};
```

### 2. Дублирование данных
```javascript
// Решение: всегда проверять существование перед сохранением
const isDuplicate = async (url) => {
  const existing = await SearchProjectArtifacts({
    query: url,
    projectId: "VSExtensibility",
    maxResults: 1
  });
  return existing.length > 0;
};
```

### 3. Слишком большие файлы
```javascript
// Решение: разбивать на логические части
const splitLargeContent = (content, maxSize = 6000) => {
  if (content.length <= maxSize) return [content];
  
  const parts = [];
  const paragraphs = content.split('\n\n');
  let currentPart = '';
  
  for (const paragraph of paragraphs) {
    if ((currentPart + paragraph).length > maxSize) {
      parts.push(currentPart);
      currentPart = paragraph;
    } else {
      currentPart += (currentPart ? '\n\n' : '') + paragraph;
    }
  }
  
  if (currentPart) parts.push(currentPart);
  return parts;
};
```

## 🎯 Быстрый чек-лист для начала

1. ✅ Открыть стартовую страницу
2. ✅ Собрать все уникальные ссылки одним скриптом
3. ✅ Сохранить реестр ссылок в память
4. ✅ Запустить цикл обработки с проверкой дубликатов
5. ✅ Мониторить прогресс каждые 5 страниц
6. ✅ Генерировать итоговый отчёт

## 💡 Советы по производительности

- **Headless режим**: ускорение в 2-3 раза
- **Оптимальный timeout**: 10-15 секунд
- **Проверка дубликатов**: экономия времени на повторные запросы
- **Пакетная обработка**: баланс скорости и стабильности
- **Логирование прогресса**: контроль процесса

---

# 🚀 Быстрый справочник по парсингу документации

## 📋 Запуск в 5 шагов

### 1. Инициализация
```javascript
// Открываем стартовую страницу
await playwright.navigate("https://learn.microsoft.com/ru-ru/visualstudio/extensibility/visualstudio.extensibility?view=vs-2022");
```

### 2. Сбор ссылок
```javascript
// Выполняем в консоли браузера
const links = Array.from(document.querySelectorAll('a[href*="visualstudio.extensibility"]'))
  .map(a => a.href)
  .filter(href => href.includes('learn.microsoft.com') && !href.includes('#'))
  .filter((href, index, array) => array.indexOf(href) === index);
console.log(links);
```

### 3. Проверка уже обработанных
```javascript
// Получаем список уже сохранённых страниц
const saved = await GetProjectArtifacts({projectId: "VSExtensibility"});
console.log(`Уже сохранено: ${saved.length} артефактов`);
```

### 4. Обработка новых страниц
```javascript
// Цикл для новых страниц
for (const url of newLinks) {
  await playwright.navigate(url);
  const content = await playwright.getVisibleText();
  const title = await playwright.evaluate('document.querySelector("h1")?.textContent || document.title');
  
  await SaveProjectArtifact({
    title: title,
    content: `**Источник:** ${url}\n\n${content}`,
    type: "Document",
    projectId: "VSExtensibility",
    tags: "visual-studio,extensibility,documentation"
  });
}
```

### 5. Мониторинг прогресса
```javascript
// Проверяем статистику
const stats = await GetMemoryStatistics({projectId: "VSExtensibility"});
console.log(`📊 Всего артефактов: ${stats.totalArtifacts}`);
```

## ⚡ Готовые команды

### Поиск дубликатов
```javascript
const checkDuplicates = async (url) => {
  const results = await SearchProjectArtifacts({
    query: url,
    projectId: "VSExtensibility"
  });
  return results.length > 0;
};
```

### Фильтрация необработанных ссылок
```javascript
const getUnprocessedLinks = async (allLinks) => {
  const processed = [];
  const artifacts = await GetProjectArtifacts({projectId: "VSExtensibility"});
  
  artifacts.forEach(artifact => {
    const match = artifact.content.match(/\*\*Источник:\*\* (https?:\/\/[^\n]+)/);
    if (match) processed.push(match[1]);
  });
  
  return allLinks.filter(link => !processed.includes(link));
};
```

### Экспресс-парсинг одной страницы
```javascript
const parsePageQuick = async (url) => {
  await playwright.navigate(url);
  const title = await playwright.evaluate('document.querySelector("h1")?.textContent || document.title.split(" | ")[0]');
  const content = await playwright.getVisibleText();
  
  return {
    title: title,
    content: `**Источник:** ${url}\n\n${content}`,
    type: "Document",
    projectId: "VSExtensibility",
    tags: "visual-studio,extensibility,documentation,quick-parse"
  };
};
```

## 🔧 Типичные ошибки и быстрые исправления

| Ошибка | Быстрое решение |
|--------|----------------|
| **Таймаут загрузки** | `await playwright.navigate(url, {timeout: 15000})` |
| **Пустой контент** | Проверить селектор: `document.body.textContent` |
| **Дубликаты** | Всегда проверять: `await checkDuplicates(url)` |
| **Слишком большой текст** | Ограничить: `content.slice(0, 8000)` |
| **Отсутствует заголовок** | Фолбэк: `document.title.split(' | ')[0]` |

## 📊 Мониторинг в реальном времени

```javascript
// Показать прогресс
const showProgress = async () => {
  const stats = await GetMemoryStatistics({projectId: "VSExtensibility"});
  const artifacts = await GetProjectArtifacts({projectId: "VSExtensibility"});
  
  console.log(`
  📈 СТАТИСТИКА ПАРСИНГА:
  - Всего артефактов: ${stats.totalArtifacts}
  - Документов: ${artifacts.filter(a => a.type === 'Document').length}
  - Справочников: ${artifacts.filter(a => a.type === 'Reference').length}
  - Последнее обновление: ${new Date().toLocaleString()}
  `);
};
```

## 🎯 Одноразовые команды для экстренных случаев

### Если нужно быстро парсить одну страницу
```javascript
// Экспресс-парсинг текущей страницы
const title = document.querySelector('h1')?.textContent || document.title;
const content = document.body.textContent;
console.log(`Заголовок: ${title}`);
console.log(`Размер контента: ${content.length} символов`);
```

### Если нужно найти все ссылки на текущей странице
```javascript
// Все ссылки на документацию
const docLinks = Array.from(document.querySelectorAll('a[href*="visualstudio.extensibility"]'))
  .map(a => ({text: a.textContent.trim(), href: a.href}))
  .filter(link => link.href.includes('learn.microsoft.com'));
console.table(docLinks);
```

### Если нужно проверить, сохранена ли уже страница
```javascript
// Быстрая проверка
const currentUrl = window.location.href;
const exists = await SearchProjectArtifacts({
  query: currentUrl,
  projectId: "VSExtensibility",
  maxResults: 1
});
console.log(exists.length > 0 ? "✅ Уже сохранена" : "❌ Нужно сохранить");
```

---

**Результат**: Полностью автоматизированный, масштабируемый и надёжный процесс парсинга документации с возможностью восстановления после сбоев и отслеживания прогресса.

**💡 Совет**: Сохраните этот справочник в закладки для быстрого доступа к командам во время парсинга!
