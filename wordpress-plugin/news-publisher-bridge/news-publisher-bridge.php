<?php
/**
 * Plugin Name: News Publisher Bridge for Diafaneia
 * Description: Secure News Publisher bridge for diafaneia.eu, with Yoast SEO mapping.
 * Version: 1.0.0
 * Author: Karago
 */

if (!defined('ABSPATH')) { exit; }

final class Karago_News_Publisher_Bridge {
    const NS = 'karago-news/v1';
    const ROUTE = '/article';
    const META_REQUEST_ID = '_karago_mobile_request_id';

    public static function init() {
        add_action('rest_api_init', [__CLASS__, 'register_routes']);
        add_action('admin_menu', [__CLASS__, 'admin_menu']);
    }

    public static function register_routes() {
        register_rest_route(self::NS, self::ROUTE, [
            'methods' => 'POST',
            'callback' => [__CLASS__, 'create_article'],
            'permission_callback' => function () {
                return is_user_logged_in() && current_user_can('edit_posts');
            },
        ]);

        register_rest_route(self::NS, '/ping', [
            'methods' => 'GET',
            'callback' => function () {
                return rest_ensure_response([
                    'ok' => true,
                    'site' => self::site_key(),
                    'user' => wp_get_current_user()->user_login,
                    'can_publish' => current_user_can('publish_posts'),
                    'version' => '1.0.0',
                ]);
            },
            'permission_callback' => function () {
                return is_user_logged_in() && current_user_can('edit_posts');
            },
        ]);
    }

    public static function site_key() {
        $host = strtolower((string) wp_parse_url(home_url(), PHP_URL_HOST));
        if ($host === 'diafaneia.eu' || $host === 'www.diafaneia.eu') return 'diafaneia';
        return sanitize_key($host);
    }

    private static function get_string(WP_REST_Request $request, $key, $default = '') {
        $value = $request->get_param($key);
        if ($value === null) return $default;
        return is_scalar($value) ? (string) $value : $default;
    }

    private static function resolve_categories($items) {
        if ($items === null || $items === '') return [];
        if (!is_array($items)) $items = [$items];
        $ids = [];
        $unknown = [];

        foreach ($items as $item) {
            if (is_numeric($item)) {
                $term = get_term((int) $item, 'category');
                if ($term && !is_wp_error($term)) $ids[] = (int) $term->term_id;
                else $unknown[] = (string) $item;
                continue;
            }

            $name = trim(wp_strip_all_tags((string) $item));
            if ($name === '') continue;
            $term = get_term_by('slug', sanitize_title($name), 'category');
            if (!$term) $term = get_term_by('name', $name, 'category');
            if ($term && !is_wp_error($term)) $ids[] = (int) $term->term_id;
            else $unknown[] = $name;
        }

        if ($unknown) {
            return new WP_Error(
                'karago_unknown_category',
                'Unknown category/categories: ' . implode(', ', $unknown) . '. Use an existing WordPress category name or slug.',
                ['status' => 400]
            );
        }
        return array_values(array_unique($ids));
    }

    private static function normalize_tags($items) {
        if ($items === null || $items === '') return [];
        if (!is_array($items)) $items = [$items];
        $tags = [];
        foreach ($items as $item) {
            $name = trim(wp_strip_all_tags((string) $item));
            if ($name !== '') $tags[] = $name;
        }
        return array_values(array_unique($tags));
    }

    private static function sanitize_content_for_user($html) {
        $html = (string) $html;
        if (current_user_can('unfiltered_html')) {
            return $html;
        }
        return wp_kses_post($html);
    }

    private static function find_existing_request($request_id) {
        if (!$request_id) return 0;
        $ids = get_posts([
            'post_type' => 'post',
            'post_status' => ['draft', 'pending', 'publish', 'future', 'private'],
            'posts_per_page' => 1,
            'fields' => 'ids',
            'meta_key' => self::META_REQUEST_ID,
            'meta_value' => $request_id,
            'orderby' => 'ID',
            'order' => 'DESC',
            'no_found_rows' => true,
        ]);
        return $ids ? (int) $ids[0] : 0;
    }

    private static function post_response($post_id, $duplicate = false) {
        $post = get_post($post_id);
        return rest_ensure_response([
            'ok' => true,
            'duplicate' => (bool) $duplicate,
            'post_id' => (int) $post_id,
            'status' => $post ? $post->post_status : '',
            'title' => $post ? get_the_title($post) : '',
            'link' => get_permalink($post_id),
            'edit_link' => get_edit_post_link($post_id, 'raw'),
            'site' => self::site_key(),
        ]);
    }

    public static function create_article(WP_REST_Request $request) {
        $site = sanitize_key(self::get_string($request, 'site'));
        $actual_site = self::site_key();
        if ($site !== 'diafaneia') {
            return new WP_Error('karago_invalid_site', 'SITE must be diafaneia.', ['status' => 400]);
        }
        if ($site !== $actual_site) {
            return new WP_Error(
                'karago_wrong_site',
                sprintf('This package is for "%s", but this endpoint belongs to "%s".', $site, $actual_site),
                ['status' => 409]
            );
        }

        $request_id = sanitize_text_field(self::get_string($request, 'request_id'));
        if ($request_id) {
            $existing = self::find_existing_request($request_id);
            if ($existing) return self::post_response($existing, true);
        }

        $title = sanitize_text_field(self::get_string($request, 'title'));
        $content = self::sanitize_content_for_user(self::get_string($request, 'content'));
        $slug_input = self::get_string($request, 'slug');
        $excerpt = self::get_string($request, 'excerpt');
        $seo_title = self::get_string($request, 'seo_title');
        $meta_description = self::get_string($request, 'meta_description');
        $focus_keyword = self::get_string($request, 'focus_keyword');
        $raw_categories = $request->get_param('categories');
        $raw_tags = $request->get_param('tags');
        if ($title === '' || trim($content) === '' || trim($slug_input) === '' || trim($excerpt) === ''
            || trim($seo_title) === '' || trim($meta_description) === '' || trim($focus_keyword) === ''
            || empty($raw_categories) || empty($raw_tags)) {
            return new WP_Error(
                'karago_missing_required',
                'Required: SITE, TITLE, SLUG, CATEGORY, TAGS, EXCERPT, FOCUS_KEYWORD, SEO_TITLE, META_DESCRIPTION and HTML.',
                ['status' => 400]
            );
        }

        $status = sanitize_key(self::get_string($request, 'status', 'draft'));
        $allowed_statuses = ['draft', 'pending', 'publish'];
        if (!in_array($status, $allowed_statuses, true)) $status = 'draft';
        if ($status === 'publish' && !current_user_can('publish_posts')) {
            return new WP_Error('karago_cannot_publish', 'This WordPress user cannot publish posts.', ['status' => 403]);
        }

        $categories = self::resolve_categories($raw_categories);
        if (is_wp_error($categories)) return $categories;
        if (!$categories) return new WP_Error('karago_missing_category', 'CATEGORY is required.', ['status' => 400]);
        $tags = self::normalize_tags($raw_tags);
        if (!$tags) return new WP_Error('karago_missing_tags', 'TAGS is required.', ['status' => 400]);
        if (trim(self::get_string($request, 'team_tag')) !== '') {
            return new WP_Error('karago_team_tag_not_supported', 'TEAM_TAG is not used on Diafaneia.', ['status' => 400]);
        }

        $meta_input = [
            '_yoast_wpseo_title' => sanitize_text_field($seo_title),
            '_yoast_wpseo_metadesc' => sanitize_textarea_field($meta_description),
            '_yoast_wpseo_focuskw' => sanitize_text_field($focus_keyword),
        ];
        if ($request_id) $meta_input[self::META_REQUEST_ID] = $request_id;

        $postarr = [
            'post_type' => 'post',
            'post_status' => $status,
            'post_title' => $title,
            'post_content' => $content,
            'post_excerpt' => sanitize_textarea_field($excerpt),
            'meta_input' => $meta_input,
        ];
        $slug = sanitize_title($slug_input);
        if ($slug === '') {
            return new WP_Error('karago_invalid_slug', 'SLUG is invalid after WordPress sanitization.', ['status' => 400]);
        }
        if ($slug) $postarr['post_name'] = $slug;
        if ($categories) $postarr['post_category'] = $categories;

        $post_id = wp_insert_post(wp_slash($postarr), true);
        if (is_wp_error($post_id)) return $post_id;

        if ($tags) {
            $tag_result = wp_set_post_tags($post_id, $tags, false);
            if (is_wp_error($tag_result)) {
                wp_delete_post($post_id, true);
                return $tag_result;
            }
        }

        $featured_media_id = absint($request->get_param('featured_media_id'));
        if ($featured_media_id) {
            $attachment = get_post($featured_media_id);
            if ($attachment && $attachment->post_type === 'attachment') {
                set_post_thumbnail($post_id, $featured_media_id);
            }
        }

        return self::post_response($post_id, false);
    }

    public static function admin_menu() {
        add_management_page(
            'Mobile Publisher',
            'Mobile Publisher',
            'edit_posts',
            'karago-mobile-publisher',
            [__CLASS__, 'admin_page']
        );
    }

    public static function admin_page() {
        if (!current_user_can('edit_posts')) return;
        $endpoint = esc_url_raw(rest_url(self::NS . self::ROUTE));
        $nonce = wp_create_nonce('wp_rest');
        $site = self::site_key();
        ?>
        <div class="wrap" style="max-width:900px">
            <h1>Mobile Publisher</h1>
            <p><strong>Site:</strong> <?php echo esc_html($site); ?> — Paste one KARAGO_ARTICLE_V1 block below.</p>
            <textarea id="karago-box" style="width:100%;min-height:52vh;font-family:monospace;font-size:15px;padding:12px" placeholder="Paste the complete article package here..."></textarea>
            <div style="display:flex;gap:10px;flex-wrap:wrap;margin-top:12px">
                <button class="button button-primary" id="karago-draft" style="min-height:44px">Save as Draft</button>
                <button class="button" id="karago-publish" style="min-height:44px">Publish</button>
                <button class="button" id="karago-clear" style="min-height:44px">Clear</button>
            </div>
            <pre id="karago-result" style="white-space:pre-wrap;background:#fff;padding:12px;border:1px solid #ccd0d4;margin-top:12px"></pre>
        </div>
        <script>
        (() => {
            const endpoint = <?php echo wp_json_encode($endpoint); ?>;
            const nonce = <?php echo wp_json_encode($nonce); ?>;
            const actualSite = <?php echo wp_json_encode($site); ?>;
            const box = document.getElementById('karago-box');
            const out = document.getElementById('karago-result');

            function parse(text) {
                text = String(text || '').replace(/\r\n/g, '\n');
                const start = text.indexOf('[KARAGO_ARTICLE_V1]');
                const htmlMark = start < 0 ? -1 : text.indexOf('---HTML---', start + '[KARAGO_ARTICLE_V1]'.length);
                const endMark = htmlMark < 0 ? -1 : text.indexOf('---END---', htmlMark + '---HTML---'.length);
                const closeMark = endMark < 0 ? -1 : text.indexOf('[/KARAGO_ARTICLE_V1]', endMark + '---END---'.length);
                if (start < 0 || htmlMark < 0 || endMark < htmlMark || closeMark < endMark) throw new Error('Invalid package. Missing KARAGO_ARTICLE_V1 / HTML markers.');
                const header = text.substring(start + '[KARAGO_ARTICLE_V1]'.length, htmlMark).trim();
                let html = text.substring(htmlMark + '---HTML---'.length, endMark);
                if (html.startsWith('\n')) html = html.substring(1);
                if (html.endsWith('\n')) html = html.substring(0, html.length - 1);
                const m = {};
                for (const line of header.split('\n')) {
                    const i = line.indexOf(':');
                    if (i <= 0) continue;
                    m[line.substring(0, i).trim().toUpperCase()] = line.substring(i + 1).trim();
                }
                const splitList = (v) => (v || '').split('|').map(s => s.trim()).filter(Boolean);
                return {
                    site: (m.SITE || '').toLowerCase(),
                    request_id: m.REQUEST_ID || (self.crypto?.randomUUID ? self.crypto.randomUUID() : String(Date.now())),
                    title: m.TITLE || '',
                    slug: m.SLUG || '',
                    excerpt: m.EXCERPT || '',
                    categories: splitList(m.CATEGORY || m.CATEGORIES),
                    tags: splitList(m.TAGS),
                    team_tag: m.TEAM_TAG || '',
                    secondary_keywords: splitList(m.SECONDARY_KEYWORDS),
                    seo_title: m.SEO_TITLE || '',
                    meta_description: m.META_DESCRIPTION || '',
                    focus_keyword: m.FOCUS_KEYWORD || '',
                    content: html,
                };
            }

            function validate(payload) {
                if (payload.site !== 'diafaneia') throw new Error('SITE must be diafaneia.');
                if (payload.site !== actualSite) throw new Error(`Wrong site: package=${payload.site}, current=${actualSite}`);
                const required = [
                    ['TITLE', payload.title], ['SLUG', payload.slug], ['CATEGORY', payload.categories.length],
                    ['TAGS', payload.tags.length], ['EXCERPT', payload.excerpt], ['FOCUS_KEYWORD', payload.focus_keyword],
                    ['SEO_TITLE', payload.seo_title], ['META_DESCRIPTION', payload.meta_description], ['HTML', payload.content]
                ];
                for (const [name, value] of required) if (!value) throw new Error(name + ' is empty.');
                if (payload.team_tag) throw new Error('TEAM_TAG is not used on Diafaneia.');
            }

            async function send(status) {
                out.textContent = 'Sending...';
                try {
                    const payload = parse(box.value);
                    validate(payload);
                    payload.status = status;
                    const r = await fetch(endpoint, {
                        method: 'POST',
                        headers: {'Content-Type':'application/json', 'X-WP-Nonce': nonce},
                        body: JSON.stringify(payload),
                        credentials: 'same-origin'
                    });
                    const data = await r.json();
                    if (!r.ok) throw new Error(data.message || ('HTTP ' + r.status));
                    out.textContent = `OK\nPost ID: ${data.post_id}\nStatus: ${data.status}\n${data.duplicate ? 'Duplicate request safely reused.\n' : ''}Edit: ${data.edit_link || ''}\nLink: ${data.link || ''}`;
                } catch (e) { out.textContent = 'ERROR: ' + e.message; }
            }
            document.getElementById('karago-draft').onclick = () => send('draft');
            document.getElementById('karago-publish').onclick = () => { if (confirm('Publish this article now?')) send('publish'); };
            document.getElementById('karago-clear').onclick = () => { box.value=''; out.textContent=''; };
        })();
        </script>
        <?php
    }
}

Karago_News_Publisher_Bridge::init();
