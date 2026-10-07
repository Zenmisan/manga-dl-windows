// Based on QuickNovel NovelFireProvider
var _NFF = 'https://novelfire.net';

function _nffSanitize(html) {
  return html
    .replace(/<script[\s\S]*?<\/script>/gi, '')
    .replace(/<iframe[\s\S]*?<\/iframe>/gi, '')
    .replace(/\son\w+="[^"]*"/gi, '')
    .replace(/\son\w+='[^']*'/gi, '');
}

function _nffParseCards(doc, provider) {
  var results = [];
  doc.querySelectorAll('li.novel-item, .novel-item').forEach(function(item) {
    var a = item.querySelector('h4.novel-title a, h5 a, a[title], a');
    if (!a) return;
    var href = a.getAttribute('href') || '';
    // href like /book/slug or https://novelfire.net/book/slug
    var slug = href.replace(/.*\/book\//, '').replace(/\/$/, '');
    if (!slug || slug.length < 2) return;
    var titleEl = item.querySelector('h4.novel-title, h5.novel-title, .novel-title') || a;
    var title = (titleEl ? titleEl.textContent : a.getAttribute('title') || a.textContent || slug).trim();
    title = title.split('\n')[0].trim();
    var img = item.querySelector('img');
    var cover = img ? (img.getAttribute('data-src') || img.getAttribute('src')) : null;
    results.push({ id: slug, title: title, cover_url: cover, provider: provider || 'novelfire', url: _NFF + '/book/' + slug, status: null });
  });
  return results;
}

function _nffParseChapterLinks(doc, novelId) {
  var chapters = [];
  doc.querySelectorAll('ul.chapter-list li a, .chapters-list li a, .list-chapter li a, a[href*="/chapter-"]').forEach(function(a) {
    var href = a.getAttribute('href') || '';
    var chMatch = href.match(/\/chapter-(\d+)/i);
    if (!chMatch) return;
    var num = parseFloat(chMatch[1]);
    var chSlug = 'chapter-' + num;
    var strongEl = a.querySelector('strong.chapter-title');
    var chTitle = (strongEl ? strongEl.textContent.trim() : null) || a.getAttribute('title') || ('Chapter ' + num);
    chTitle = chTitle.replace(/\d+\s+(?:days?|hours?|months?|years?)\s+ago/i, '').trim();
    var timeEl = a.querySelector('time.chapter-update');
    var pubDate = timeEl ? (timeEl.getAttribute('datetime') || timeEl.textContent.trim()) : null;
    chapters.push({
      id: novelId + '/' + chSlug,
      title: chTitle,
      number: num,
      published_at: pubDate,
    });
  });
  return chapters;
}

var extension = {
  async search(query, page) {
    var url = _NFF + '/search/?keyword=' + encodeURIComponent(query.trim()) + '&page=' + (page || 1);
    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(url));
    var doc = new DOMParser().parseFromString(data.html, 'text/html');
    return _nffParseCards(doc, 'novelfire');
  },

  async getMangaDetail(novelId) {
    var slug = novelId.replace(/^\//, '').replace(/\/$/, '');
    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(_NFF + '/book/' + slug));
    var doc = new DOMParser().parseFromString(data.html, 'text/html');

    var titleEl = doc.querySelector('div.novel-info h1.novel-title, h1.novel-title, h1');
    var title = titleEl ? titleEl.textContent.trim() : slug;

    var img = doc.querySelector('figure.cover img, .cover img');
    var cover = img ? (img.getAttribute('src') || img.getAttribute('data-src')) : null;

    var desc = doc.querySelector('meta[itemprop="description"]')?.getAttribute('content') || '';

    var genres = [];
    doc.querySelectorAll('div.categories ul li, div.tags ul li').forEach(function(el) {
      var g = el.textContent.trim();
      if (g) genres.push(g);
    });

    var status = null;
    doc.querySelectorAll('div.header-stats span').forEach(function(span) {
      if (/status/i.test(span.textContent)) {
        var strong = span.querySelector('strong');
        if (strong) status = strong.textContent.trim();
      }
    });

    var authorEl = doc.querySelector('div.novel-info div.author a, div.author a');
    var authors = authorEl ? [authorEl.textContent.trim()] : [];

    // Attempt 1: AJAX chapter list
    var chapters = [];
    var reportEl = doc.querySelector('a#novel-report');
    var postId = reportEl ? reportEl.getAttribute('report-post_id') : null;

    if (postId) {
      var ajaxParams = [
        'draw=1',
        'columns%5B0%5D%5Bdata%5D=n_sort',
        'columns%5B0%5D%5Bname%5D=cmm_posts_detail.n_sort',
        'columns%5B0%5D%5Bsearchable%5D=true',
        'columns%5B0%5D%5Borderable%5D=true',
        'columns%5B0%5D%5Bsearch%5D%5Bvalue%5D=',
        'columns%5B0%5D%5Bsearch%5D%5Bregex%5D=false',
        'order%5B0%5D%5Bcolumn%5D=0',
        'order%5B0%5D%5Bdir%5D=asc',
        'start=0',
        'length=-1',
        'search%5Bvalue%5D=',
        'search%5Bregex%5D=false',
        'post_id=' + encodeURIComponent(postId),
        'only_bookmark=false',
      ].join('&');
      try {
        var chData = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(_NFF + '/ajax/listChapterDataAjax?' + ajaxParams));
        var parsed = null;
        try { parsed = JSON.parse(chData.html || chData.text || ''); } catch(e) {}
        if (parsed && Array.isArray(parsed.data) && parsed.data.length > 0) {
          parsed.data.forEach(function(item) {
            var n = item.n_sort;
            if (!n) return;
            var chTitle = item.title || ('Chapter ' + n);
            var chId = slug + '/chapter-' + n;
            chapters.push({ id: chId, title: chTitle, number: n, published_at: item.bookmark_created_at || null });
          });
        }
      } catch(e) {}
    }

    // Attempt 2: HTML chapters fallback
    if (chapters.length === 0) {
      try {
        var chPage1 = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(_NFF + '/book/' + slug + '/chapters?page=1'));
        var doc1 = new DOMParser().parseFromString(chPage1.html || '', 'text/html');
        var page1Chapters = _nffParseChapterLinks(doc1, slug);
        page1Chapters.forEach(function(ch) { chapters.push(ch); });

        // Check max page
        var maxPage = 1;
        doc1.querySelectorAll('ul.pagination a, .pagination a').forEach(function(a) {
          var m = (a.getAttribute('href') || '').match(/page=(\d+)/i);
          if (m) {
            var p = parseInt(m[1], 10);
            if (p > maxPage) maxPage = p;
          }
        });

        if (maxPage > 1) {
          var pagePromises = [];
          for (var p = 2; p <= Math.min(maxPage, 20); p++) {
            pagePromises.push(apiFetch('/manga/proxy/html?url=' + encodeURIComponent(_NFF + '/book/' + slug + '/chapters?page=' + p)));
          }
          var extraPages = await Promise.all(pagePromises);
          extraPages.forEach(function(ep) {
            var epDoc = new DOMParser().parseFromString(ep.html || '', 'text/html');
            var epChapters = _nffParseChapterLinks(epDoc, slug);
            epChapters.forEach(function(ch) { chapters.push(ch); });
          });
        }
      } catch(e) {}
    }

    // Sort ascending by chapter number, deduplicate by id
    var seen = {};
    var uniqueChapters = [];
    chapters.forEach(function(ch) {
      if (!seen[ch.id]) {
        seen[ch.id] = true;
        uniqueChapters.push(ch);
      }
    });
    uniqueChapters.sort(function(a, b) { return a.number - b.number; });

    return { id: slug, title: title, cover_url: cover, description: desc, status: status, genres: genres, authors: authors, provider: 'novelfire', url: _NFF + '/book/' + slug, chapters: uniqueChapters };
  },

  async getChapterText(chapterId) {
    var parts = chapterId.split('/');
    var slug = parts[0];
    var chPart = parts.slice(1).join('/');
    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(_NFF + '/book/' + slug + '/' + chPart));
    var doc = new DOMParser().parseFromString(data.html, 'text/html');
    var contentEl = doc.querySelector('div#content, .chapter-content, #chr-content');
    if (contentEl) {
      var firstP = contentEl.querySelector('p');
      var titleEl = doc.querySelector('span.chapter-title');
      if (firstP && titleEl && firstP.textContent.trim().toLowerCase() === titleEl.textContent.trim().toLowerCase()) {
        firstP.remove();
      }
      contentEl.querySelectorAll('img[src*="disable-blocker"]').forEach(function(el) { el.remove(); });
    }
    var content = contentEl ? contentEl.innerHTML : '<p>Chapter content not found.</p>';
    return { content: _nffSanitize(content), format: 'html' };
  },

  async getPopular(page) {
    var url = _NFF + '/genre-all/sort-popular/status-all/all-novel?page=' + (page || 1);
    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(url));
    var doc = new DOMParser().parseFromString(data.html, 'text/html');
    return _nffParseCards(doc, 'novelfire');
  },

  async getLatest(page) {
    var url = _NFF + '/genre-all/sort-latest-release/status-all/all-novel?page=' + (page || 1);
    var data = await apiFetch('/manga/proxy/html?url=' + encodeURIComponent(url));
    var doc = new DOMParser().parseFromString(data.html, 'text/html');
    return _nffParseCards(doc, 'novelfire');
  },
};
