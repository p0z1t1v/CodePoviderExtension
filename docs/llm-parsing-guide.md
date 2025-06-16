# 🤖 Универсальная инструкция: Автоматизированный парсинг документации для LLM

> **Целевая аудитория**: Языковые модели (Claude, GPT, Gemini и др.) с доступом к инструментам веб-автоматизации и хранения данных

## 🎯 Обзор задачи

**Цель**: Автоматизированный сбор, структурирование и сохранение больших объёмов веб-документации в базу знаний для последующего использования в контексте разговоров.

**Применение**: Парсинг технической документации, API-справочников, руководств пользователя, образовательных материалов и других структурированных веб-ресурсов.

## 🛠️ Требуемые инструменты

### Обязательные возможности:
- ✅ **Веб-автоматизация** (Playwright, Selenium, Puppeteer)
- ✅ **Хранение данных** (MCP memory, векторные БД, файловая система)
- ✅ **JavaScript выполнение** в браузере
- ✅ **Извлечение текста** со страниц

### Опциональные улучшения:
- 🔄 **Параллельная обработка** для ускорения
- 📊 **Аналитика и мониторинг** прогресса
- 🧠 **Семантический поиск** для дедупликации
- 📝 **Структурирование контента** с метаданными

## 📋 Универсальный алгоритм

### Фаза 1: Разведка и планирование

```python
# Псевдокод для любой LLM
def analyze_target_site(start_url):
    """
    Анализируем структуру сайта и планируем стратегию парсинга
    """
    # 1. Открываем стартовую страницу
    navigate_to(start_url)
    
    # 2. Анализируем структуру навигации
    navigation_patterns = extract_navigation_structure()
    
    # 3. Находим все релевантные ссылки
    all_links = discover_all_documentation_links()
    
    # 4. Фильтруем и дедуплицируем
    unique_links = filter_and_deduplicate(all_links)
    
    # 5. Сохраняем план парсинга
    save_parsing_plan(unique_links, navigation_patterns)
    
    return unique_links
```

### Фаза 2: Массовый сбор данных

```python
def execute_mass_parsing(links_list, storage_config):
    """
    Выполняем массовый парсинг с оптимизацией
    """
    results = []
    
    for i, url in enumerate(links_list):
        try:
            # Проверяем, не обработана ли уже страница
            if is_already_processed(url, storage_config):
                log(f"[{i+1}/{len(links_list)}] Пропускаем {url} - уже обработан")
                continue
            
            # Парсим страницу
            page_data = parse_single_page(url)
            
            # Структурируем данные
            structured_data = structure_content(page_data)
            
            # Сохраняем в хранилище
            save_to_storage(structured_data, storage_config)
            
            # Логируем прогресс
            log(f"[{i+1}/{len(links_list)}] ✅ Обработан: {page_data.title}")
            
            # Пауза для стабильности
            sleep(1)
            
        except Exception as error:
            log(f"❌ Ошибка при обработке {url}: {error}")
            continue
    
    return results
```

### Фаза 3: Контроль качества и аналитика

```python
def validate_and_analyze_results(storage_config):
    """
    Проверяем качество собранных данных
    """
    # Получаем статистику
    stats = get_storage_statistics(storage_config)
    
    # Ищем дубликаты
    duplicates = find_duplicate_content(storage_config)
    
    # Проверяем полноту покрытия
    coverage_report = analyze_coverage(storage_config)
    
    # Генерируем отчёт
    final_report = generate_final_report(stats, duplicates, coverage_report)
    
    return final_report
```

## 🚀 Реализация для различных инструментов

### Для MCP (Model Context Protocol)

```javascript
// Специфичная реализация для MCP my-memory
class MCPDocumentationParser {
    constructor(projectId) {
        this.projectId = projectId;
    }
    
    async parseDocumentationSite(startUrl) {
        // 1. Собираем ссылки
        const links = await this.collectAllLinks(startUrl);
        
        // 2. Обрабатываем каждую страницу
        for (const [index, url] of links.entries()) {
            await this.processSinglePage(url, index + 1, links.length);
        }
        
        // 3. Генерируем отчёт
        return await this.generateReport();
    }
    
    async collectAllLinks(startUrl) {
        await playwright_navigate(startUrl);
        
        const links = await playwright_evaluate(`
            Array.from(document.querySelectorAll('a[href*="documentation"]'))
                .map(a => a.href)
                .filter(href => href.includes('your-target-domain'))
                .filter((href, i, arr) => arr.indexOf(href) === i)
        `);
        
        // Сохраняем реестр ссылок
        await mcp_memory_SaveProjectArtifact({
            title: "Реестр ссылок для парсинга",
            type: "Reference",
            content: JSON.stringify(links, null, 2),
            projectId: this.projectId,
            tags: "parsing,links,registry"
        });
        
        return links;
    }
    
    async processSinglePage(url, index, total) {
        // Проверяем дубликаты
        const existing = await mcp_memory_SearchProjectArtifacts({
            query: url,
            projectId: this.projectId,
            maxResults: 1
        });
        
        if (existing.length > 0) {
            console.log(`[${index}/${total}] Пропускаем ${url} - уже обработан`);
            return;
        }
        
        // Парсим страницу
        await playwright_navigate(url);
        const content = await playwright_get_visible_text();
        const title = await playwright_evaluate(`
            document.querySelector('h1')?.textContent || 
            document.title.split(' | ')[0]
        `);
        
        // Сохраняем
        await mcp_memory_SaveProjectArtifact({
            title: title,
            type: "Document",
            content: `# ${title}\n\n**Источник:** ${url}\n\n${content}`,
            projectId: this.projectId,
            tags: "documentation,auto-parsed,web-content"
        });
        
        console.log(`[${index}/${total}] ✅ Сохранено: ${title}`);
    }
}
```

### Для файловой системы

```python
# Реализация для сохранения в локальные файлы
import os
import json
import hashlib
from urllib.parse import urlparse

class FileSystemDocumentationParser:
    def __init__(self, output_dir):
        self.output_dir = output_dir
        os.makedirs(output_dir, exist_ok=True)
        os.makedirs(f"{output_dir}/pages", exist_ok=True)
        os.makedirs(f"{output_dir}/meta", exist_ok=True)
    
    def parse_documentation_site(self, start_url):
        # Собираем ссылки
        links = self.collect_all_links(start_url)
        
        # Сохраняем реестр
        with open(f"{self.output_dir}/meta/links_registry.json", 'w', encoding='utf-8') as f:
            json.dump(links, f, ensure_ascii=False, indent=2)
        
        # Обрабатываем страницы
        for i, url in enumerate(links):
            self.process_single_page(url, i + 1, len(links))
        
        # Генерируем индекс
        self.generate_content_index()
    
    def process_single_page(self, url, index, total):
        # Генерируем безопасное имя файла
        url_hash = hashlib.md5(url.encode()).hexdigest()[:8]
        safe_name = self.url_to_filename(url)
        filename = f"{safe_name}_{url_hash}.md"
        
        file_path = f"{self.output_dir}/pages/{filename}"
        
        # Проверяем, не обработан ли уже
        if os.path.exists(file_path):
            print(f"[{index}/{total}] Пропускаем {url} - уже обработан")
            return
        
        # Парсим содержимое (здесь используйте ваши веб-инструменты)
        title, content = self.extract_page_content(url)
        
        # Формируем markdown
        markdown_content = f"""# {title}

**Источник:** {url}  
**Обработано:** {datetime.now().isoformat()}

---

{content}
"""
        
        # Сохраняем файл
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(markdown_content)
        
        print(f"[{index}/{total}] ✅ Сохранено: {title}")
    
    def url_to_filename(self, url):
        # Преобразуем URL в безопасное имя файла
        parsed = urlparse(url)
        path_parts = parsed.path.strip('/').split('/')
        return '_'.join(part for part in path_parts if part)[:50]
```

### Для векторных баз данных

```python
# Реализация для Pinecone, Weaviate, ChromaDB и др.
class VectorDBDocumentationParser:
    def __init__(self, vector_client, collection_name):
        self.vector_client = vector_client
        self.collection_name = collection_name
    
    def parse_with_embeddings(self, start_url):
        links = self.collect_all_links(start_url)
        
        for url in links:
            # Проверяем существование по URL
            if self.document_exists(url):
                continue
            
            # Парсим контент
            title, content = self.extract_page_content(url)
            
            # Генерируем эмбеддинги
            embedding = self.generate_embedding(f"{title}\n{content}")
            
            # Сохраняем в векторную БД
            self.vector_client.upsert([{
                'id': self.url_to_id(url),
                'values': embedding,
                'metadata': {
                    'title': title,
                    'url': url,
                    'content': content[:1000],  # Первые 1000 символов
                    'full_content': content,
                    'parsed_at': datetime.now().isoformat()
                }
            }])
    
    def semantic_search(self, query, top_k=5):
        # Поиск по семантическому сходству
        query_embedding = self.generate_embedding(query)
        results = self.vector_client.query(
            vector=query_embedding,
            top_k=top_k,
            include_metadata=True
        )
        return results
```

## 🎛️ Конфигурация и настройки

### Универсальная конфигурация

```json
{
  "parsing_config": {
    "target_site": "https://docs.example.com",
    "batch_size": 5,
    "delay_between_requests": 1000,
    "max_retries": 3,
    "timeout": 15000,
    "headless_mode": true
  },
  "content_filters": {
    "min_content_length": 100,
    "max_content_length": 50000,
    "exclude_patterns": ["login", "signup", "404"],
    "include_patterns": ["docs", "guide", "api", "tutorial"]
  },
  "storage_config": {
    "type": "mcp|filesystem|vectordb",
    "project_id": "DocumentationProject",
    "output_directory": "./parsed_docs",
    "chunk_size": 1000,
    "metadata_fields": ["title", "url", "tags", "parsed_at"]
  },
  "quality_control": {
    "check_duplicates": true,
    "validate_links": true,
    "generate_reports": true,
    "auto_cleanup": false
  }
}
```

## 📊 Мониторинг и отчётность

### Универсальная система метрик

```python
class ParsingMetrics:
    def __init__(self):
        self.stats = {
            'total_links_found': 0,
            'pages_processed': 0,
            'pages_skipped': 0,
            'errors_encountered': 0,
            'total_content_size': 0,
            'average_page_size': 0,
            'processing_time': 0,
            'start_time': None,
            'end_time': None
        }
    
    def log_progress(self, current, total, page_title=""):
        progress = (current / total) * 100
        print(f"📊 Прогресс: {current}/{total} ({progress:.1f}%) - {page_title}")
    
    def generate_final_report(self):
        self.stats['end_time'] = datetime.now()
        self.stats['processing_time'] = (
            self.stats['end_time'] - self.stats['start_time']
        ).total_seconds()
        
        report = f"""
# 📈 Отчёт о парсинге документации

## Основные метрики
- **Обработано страниц**: {self.stats['pages_processed']}
- **Пропущено страниц**: {self.stats['pages_skipped']}
- **Ошибок**: {self.stats['errors_encountered']}
- **Общий размер контента**: {self.stats['total_content_size']:,} символов
- **Среднее время на страницу**: {self.stats['processing_time'] / max(1, self.stats['pages_processed']):.2f} секунд

## Временные рамки
- **Начало**: {self.stats['start_time']}
- **Окончание**: {self.stats['end_time']}
- **Общее время**: {self.stats['processing_time']:.1f} секунд

## Эффективность
- **Успешность**: {(self.stats['pages_processed'] / max(1, self.stats['total_links_found']) * 100):.1f}%
- **Скорость**: {self.stats['pages_processed'] / max(1, self.stats['processing_time'] / 3600):.1f} страниц/час
"""
        return report
```

## 🚨 Обработка ошибок и восстановление

### Универсальные стратегии

```python
class RobustParser:
    def __init__(self, config):
        self.config = config
        self.failed_urls = []
        self.checkpoint_frequency = 10
    
    def parse_with_recovery(self, urls):
        for i, url in enumerate(urls):
            try:
                self.process_page_with_retries(url)
                
                # Создаём чекпоинт каждые N страниц
                if i % self.checkpoint_frequency == 0:
                    self.save_checkpoint(i, urls[i:])
                    
            except Exception as e:
                self.handle_parsing_error(url, e)
                continue
    
    def process_page_with_retries(self, url, max_retries=3):
        for attempt in range(max_retries):
            try:
                return self.process_single_page(url)
            except Exception as e:
                if attempt == max_retries - 1:
                    raise e
                print(f"⚠️ Попытка {attempt + 1} для {url}: {e}")
                time.sleep(2 ** attempt)  # Экспоненциальная задержка
    
    def resume_from_checkpoint(self, checkpoint_file):
        # Восстанавливаем работу с места остановки
        with open(checkpoint_file, 'r') as f:
            remaining_urls = json.load(f)
        return remaining_urls
```

## 🔍 Специализированные случаи использования

### 1. API документация

```python
def parse_api_documentation(api_docs_url):
    """
    Специфичные настройки для API документации
    """
    config = {
        'selectors': {
            'endpoint_title': 'h2, h3',
            'endpoint_description': '.description, .summary',
            'code_examples': 'pre code, .code-block',
            'parameters': '.parameters table, .params'
        },
        'structure': {
            'extract_examples': True,
            'preserve_code_formatting': True,
            'group_by_sections': True
        }
    }
    return config
```

### 2. Учебные материалы

```python
def parse_educational_content(course_url):
    """
    Настройки для образовательного контента
    """
    config = {
        'content_types': ['lesson', 'tutorial', 'exercise', 'quiz'],
        'metadata_extraction': {
            'difficulty_level': '.difficulty, .level',
            'duration': '.duration, .time-to-complete',
            'prerequisites': '.prerequisites, .requirements'
        },
        'sequential_processing': True  # Сохраняем порядок уроков
    }
    return config
```

## 💡 Оптимизация производительности

### Универсальные советы

1. **Используйте headless режим** - ускорение в 2-3 раза
2. **Настройте оптимальные таймауты** - баланс скорости и надёжности
3. **Проверяйте дубликаты** перед обработкой
4. **Логируйте прогресс** для возможности восстановления
5. **Используйте пакетную обработку** для больших объёмов
6. **Кэшируйте результаты** промежуточной обработки
7. **Мониторьте использование ресурсов** (память, CPU, сеть)

### Контрольный список перед запуском

- [ ] Проверен доступ к целевому сайту
- [ ] Настроены инструменты веб-автоматизации
- [ ] Сконфигурировано хранилище данных
- [ ] Установлены таймауты и retry-политики
- [ ] Подготовлены системы логирования
- [ ] Запланировано время выполнения
- [ ] Подготовлен план восстановления после сбоев

---

## 🎯 Заключение

Эта инструкция предоставляет **универсальную методологию** для автоматизированного парсинга документации любой LLM с соответствующими инструментами.

**Ключевые принципы**:
- 🔄 **Адаптивность** - подходит для разных инструментов и платформ
- 🛡️ **Надёжность** - обработка ошибок и восстановление
- 📈 **Масштабируемость** - от единичных страниц до целых сайтов
- 📊 **Прозрачность** - полный контроль и мониторинг процесса

Адаптируйте код под ваши конкретные инструменты и требования!
