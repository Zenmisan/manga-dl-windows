// Based on QuickNovel NovelBuddyProvider
var _NB = 'https://novelbuddy.me';
var _NBA = 'https://api.novelbuddy.me/titles';

function _nbSanitize(html) {
  return html
    .replace(/<script[\s\S]*?<\/script>/gi, '')
    .replace(/<iframe[\s\S]*?<\/iframe>/gi, '')
    .replace(/\son\w+="[^"]*"/gi, '')
    .replace(/\son\w+='[^']*'/gi, '');
}

function _nbExtractItems(json) {
  if (!json) return [];
  if (json.data && Array.isArray(json.data.items)) return json.data.items;
  if (Array.isArray(json.data)) return json.data;
  if (Array.isArray(json.titles)) return json.titles;
  if (Array.isArray(json)) return json;
  return [];
}

var extension = {
  async search(query, page) {
    var url = _NBA + '/search?page=' + (page || 1) + '&limit=20&q=' + encodeURIComponent(query);
    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(url));
    var json = null;
    try { json = JSON.parse(data.html || data.text || ''); } catch(e) {}
    var items = _nbExtractItems(json);
    return items.map(function(item) {
      var slug = item.slug || item.id || '';
      var title = item.title || item.name || slug;
      var cover = item.cover || item.cover_url || item.image || item.thumbnail || null;
      var status = item.status ? (item.status.charAt(0).toUpperCase() + item.status.slice(1)) : null;
      return { id: slug, title: title, cover_url: cover, provider: 'novelbuddy', url: _NB + '/' + slug, status: status };
    });
  },

  async getMangaDetail(novelId) {
    var slug = novelId.replace(/^\//, '').replace(/\/$/, '');
    var url = _NB + '/' + slug;
    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(url));
    var doc = new DOMParser().parseFromString(data.html, 'text/html');

    var jsonNode = doc.querySelector('script#__NEXT_DATA__');
    var jsonText = jsonNode ? jsonNode.textContent : null;
    var json = null;
    if (jsonText) { try { json = JSON.parse(jsonText); } catch(e) {} }

    var title = slug;
    var cover = null;
    var desc = null;
    var genres = [];
    var authors = [];
    var bookId = slug;
    var status = null;

    if (json && json.props && json.props.pageProps) {
      var pp = json.props.pageProps;
      var book = pp.initialManga || pp.book || pp.title || pp.novel || null;
      if (book) {
        title = book.name || book.title || slug;
        cover = book.cover || book.cover_url || book.image || book.thumbnail || null;
        desc = book.summary || book.description || book.synopsis || null;
        bookId = book.id || slug;
        status = book.status ? (book.status.charAt(0).toUpperCase() + book.status.slice(1)) : null;
        if (Array.isArray(book.genres)) {
          genres = book.genres.map(function(g) { return g.name || g; });
        }
        if (Array.isArray(book.authors)) {
          authors = book.authors.map(function(a) { return a.name || a; });
        }
      }
    }

    var chapters = [];
    try {
      var chData = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(_NBA + '/' + bookId + '/chapters?page=1&limit=500'));
      var chJson = null;
      try { chJson = JSON.parse(chData.html || chData.text || ''); } catch(e) {}
      var chArr = (chJson && chJson.data && chJson.data.chapters) || (chJson && chJson.data) || (chJson && chJson.chapters) || [];
      if (Array.isArray(chArr)) {
        chArr.forEach(function(ch, i) {
          var chSlug = ch.slug || ch.id || (i + 1);
          var chTitle = ch.name || ch.title || ('Chapter ' + (i + 1));
          var chId = slug + '/' + chSlug;
          var num = ch.number !== undefined ? ch.number : (i + 1);
          chapters.push({ id: chId, title: chTitle, number: num, published_at: ch.createdAt || ch.created_at || ch.date || null });
        });
      }
    } catch(e) {}

    // Sort ascending by chapter number
    chapters.sort(function(a, b) { return a.number - b.number; });

    return { id: slug, title: title, cover_url: cover, description: desc, status: status, genres: genres, authors: authors, provider: 'novelbuddy', url: url, chapters: chapters };
  },

  async getChapterText(chapterId) {
    var parts = chapterId.split('/');
    var slug = parts[0];
    var chSlug = parts.slice(1).join('/');
    var url = _NB + '/' + slug + '/' + chSlug;
    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(url));
    var doc = new DOMParser().parseFromString(data.html, 'text/html');
    var contentEl = doc.querySelector('div.novel-tts-content, div.chapter-content, .content, #content');
    if (contentEl) {
      contentEl.querySelectorAll('script, style, .ads, .adsbygoogle').forEach(function(el) { el.remove(); });
    }
    var content = contentEl ? contentEl.innerHTML : '<p>Chapter content not found.</p>';
    return { content: _nbSanitize(content), format: 'html' };
  },

  async getPopular(page) {
    var url = _NBA + '?page=' + (page || 1) + '&limit=24&sort=views';
    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(url));
    var json = null;
    try { json = JSON.parse(data.html || data.text || ''); } catch(e) {}
    var items = _nbExtractItems(json);
    return items.map(function(item) {
      var s = item.slug || item.id || '';
      return { id: s, title: item.title || item.name || s, cover_url: item.cover || item.cover_url || item.image || null, provider: 'novelbuddy', url: _NB + '/' + s, status: null };
    });
  },

  async getLatest(page) {
    var url = _NBA + '?page=' + (page || 1) + '&limit=24&sort=updated';
    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(url));
    var json = null;
    try { json = JSON.parse(data.html || data.text || ''); } catch(e) {}
    var items = _nbExtractItems(json);
    return items.map(function(item) {
      var s = item.slug || item.id || '';
      return { id: s, title: item.title || item.name || s, cover_url: item.cover || item.cover_url || item.image || null, provider: 'novelbuddy', url: _NB + '/' + s, status: null };
    });
  },
};
