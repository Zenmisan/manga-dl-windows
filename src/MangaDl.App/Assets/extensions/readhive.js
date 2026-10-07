// Ported from QuickNovel ReadhiveProvider.kt
// Base URL: https://readhive.org
var _BASE = 'https://readhive.org';

function _rhSanitize(html) {
  if (!html) return '';
  return html
    .replace(/<script[\s\S]*?<\/script>/gi, '')
    .replace(/<style[\s\S]*?<\/style>/gi, '')
    .replace(/<iframe[\s\S]*?<\/iframe>/gi, '')
    .replace(/\son\w+="[^"]*"/gi, '')
    .replace(/\son\w+='[^']*'/gi, '');
}

function _fixUrl(url) {
  if (!url) return null;
  if (url.startsWith('http://') || url.startsWith('https://')) return url;
  if (url.startsWith('//')) return 'https:' + url;
  if (url.startsWith('/')) return _BASE + url;
  return _BASE + '/' + url;
}

function _rhParseDoc(html) {
  return new DOMParser().parseFromString(html, 'text/html');
}

var extension = {
  async search(query, page) {
    if (!query || !query.trim()) return [];
    var q = query.trim();

    try {
      var body = 'query=' + encodeURIComponent(q) + '&action=search';
      var res = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(_BASE + '/ajax'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: body,
      });

      var data = [];
      try {
        var parsed = JSON.parse(res.html || '{}');
        if (parsed && Array.isArray(parsed.data)) {
          data = parsed.data;
        }
      } catch (e) {}

      var results = [];
      data.forEach(function(item) {
        var href = item.url || '';
        var match = href.match(/\/series\/(\d+)/);
        var id = match ? match[1] : (item.id ? String(item.id) : null);
        if (!id) return;

        results.push({
          id: id,
          title: item.title || ('Novel ' + id),
          cover_url: _fixUrl(item.thumb),
          provider: 'readhive',
          url: _BASE + '/series/' + id + '/',
          status: null,
          type: 'novel',
        });
      });

      return results;
    } catch (err) {
      console.warn('ReadHive search failed:', err);
      return [];
    }
  },

  async getMangaDetail(novelId) {
    var cleanId = String(novelId).replace(/[^0-9]/g, '');
    var url = _BASE + '/series/' + cleanId + '/';
    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(url));
    var doc = _rhParseDoc(data.html);

    var titleEl = doc.querySelector('h1, .entry-title');
    var title = titleEl ? titleEl.textContent.trim() : ('Novel ' + cleanId);

    var img = doc.querySelector('img.object-cover, .poster img, img[alt*="thumbnail"]');
    var cover = img ? (img.getAttribute('src') || img.getAttribute('data-src')) : null;

    var descParts = [];
    doc.querySelectorAll('div.mb-4 > p, section div.mb-4 p, .description p').forEach(function(p) {
      var t = p.textContent.trim();
      if (t) descParts.push(t);
    });
    var desc = descParts.join('\n\n') || null;

    var authorEl = doc.querySelector('span.leading-7, .author, span:contains("Author")');
    var author = authorEl ? authorEl.textContent.trim() : null;

    var genres = [];
    doc.querySelectorAll('a[href*="/genre/"]').forEach(function(a) {
      var g = a.textContent.trim();
      if (g && !genres.includes(g)) genres.push(g);
    });

    var chapters = [];
    var seen = {};
    doc.querySelectorAll('a[href*="/series/' + cleanId + '/"]').forEach(function(a) {
      var href = (a.getAttribute('href') || '').replace(/\/+$/, '');
      var parts = href.split('/');
      var lastPart = parts[parts.length - 1];
      if (!lastPart || lastPart === cleanId || lastPart === 'series') return;
      if (seen[lastPart]) return;
      seen[lastPart] = true;

      var chTitleSpan = a.querySelector('div > div > span, span') || a;
      var rawName = chTitleSpan ? chTitleSpan.textContent.trim() : ('Chapter ' + lastPart);
      var chTitle = rawName.replace(/\d+\s+(years?|months?|weeks?|days?|hours?)\s+ago/i, '').trim();

      var numMatch = lastPart.match(/(\d+)/);
      var chNum = numMatch ? parseFloat(numMatch[1]) : 0;

      chapters.push({
        id: cleanId + '/' + lastPart,
        title: chTitle || ('Chapter ' + lastPart),
        number: chNum,
        published_at: null,
      });
    });

    chapters.sort(function(a, b) {
      return a.number - b.number;
    });

    return {
      id: cleanId,
      title: title,
      cover_url: _fixUrl(cover),
      description: desc,
      status: null,
      genres: genres,
      authors: author ? [author] : [],
      provider: 'readhive',
      url: url,
      type: 'novel',
      chapters: chapters,
    };
  },

  async getChapterText(chapterId) {
    var path = String(chapterId).replace(/^\/+/, '');
    var url = path.startsWith('http') ? path : (_BASE + '/series/' + path);

    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(url));
    var doc = _rhParseDoc(data.html);

    var contentEl = doc.querySelector('main > div.prose, main div[class*="prose"], div.prose, #chapter-content, .entry-content');
    if (!contentEl) {
      contentEl = doc.querySelector('main');
    }

    if (!contentEl) {
      throw new Error('Chapter content could not be located on ReadHive.');
    }

    contentEl.querySelectorAll('script, style, iframe, button, noscript, .advertisement, [class*="ad-"]').forEach(function(el) {
      el.remove();
    });

    var cleanHtml = _rhSanitize(contentEl.innerHTML);
    var titleEl = doc.querySelector('h1, h2.chapter-title, .entry-title');
    var title = titleEl ? titleEl.textContent.trim() : '';

    return {
      content: cleanHtml,
      title: title,
    };
  },

  async getPopular(page) {
    var p = page || 1;
    var url = _BASE + '/page/' + p + '/';
    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(url));
    var doc = _rhParseDoc(data.html);

    var results = [];
    var seen = {};
    doc.querySelectorAll('a.peer, a.col-span-2, a[href*="/series/"]').forEach(function(a) {
      var href = a.getAttribute('href') || '';
      var m = href.match(/\/series\/(\d+)/);
      if (!m) return;
      var id = m[1];
      if (seen[id]) return;
      seen[id] = true;

      var img = a.querySelector('img');
      var rawTitle = img ? (img.getAttribute('alt') || '') : '';
      var title = rawTitle.replace(/\s+thumbnail$/i, '').trim() || (a.textContent || '').trim() || ('Novel ' + id);

      results.push({
        id: id,
        title: title,
        cover_url: _fixUrl(img ? (img.getAttribute('src') || img.getAttribute('data-src')) : null),
        provider: 'readhive',
        url: _BASE + '/series/' + id + '/',
        status: null,
        type: 'novel',
      });
    });

    return results;
  },

  async getLatest(page) {
    return this.getPopular(page);
  },
};
