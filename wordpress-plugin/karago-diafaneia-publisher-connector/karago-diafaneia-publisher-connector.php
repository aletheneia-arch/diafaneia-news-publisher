<?php
/**
 * Plugin Name: KARAGO Diafaneia Publisher Connector
 * Description: Ιδιωτική, ασφαλής σύνδεση των εφαρμογών KARAGO με το diafaneia.eu, με ξεχωριστό κλειδί ανά συσκευή.
 * Version: 1.0.0
 * Author: KARAGO
 * Requires at least: 6.0
 * Requires PHP: 7.4
 */

if (!defined('ABSPATH')) {
    exit;
}

final class KARAGO_Diafaneia_Publisher_Connector
{
    const VERSION = '1.0.0';
    const SCHEMA_VERSION = '1.0.0';
    const REST_NAMESPACE = 'karago-diafaneia/v1';
    const SITE_KEY = 'diafaneia';
    const EXPECTED_HOST = 'diafaneia.eu';
    const HEADER_NAME = 'X-KARAGO-Key';
    const OPTION_SCHEMA_VERSION = 'karago_diafaneia_connector_schema_version';
    const META_REQUEST_ID = '_karago_diafaneia_request_id';
    const META_PAYLOAD_HASH = '_karago_diafaneia_payload_hash';
    const META_DEVICE_ID = '_karago_diafaneia_device_id';
    const MAX_ACTIVE_KEYS = 100;
    const MAX_IMAGE_BYTES = 20971520;
    const MAX_TOTAL_IMAGE_BYTES = 52428800;
    const MAX_INLINE_IMAGES = 20;
    const PROCESSING_STALE_SECONDS = 600;

    private static $current_device = null;

    public static function init()
    {
        add_action('plugins_loaded', array(__CLASS__, 'maybe_install_schema'));
        add_action('admin_menu', array(__CLASS__, 'admin_menu'));
        add_action('admin_notices', array(__CLASS__, 'wrong_site_notice'));
        add_action('admin_post_karago_diafaneia_create_device', array(__CLASS__, 'admin_create_device'));
        add_action('admin_post_karago_diafaneia_revoke_device', array(__CLASS__, 'admin_revoke_device'));
        add_action('rest_api_init', array(__CLASS__, 'register_routes'));
    }

    public static function activate()
    {
        self::install_schema();
    }

    public static function maybe_install_schema()
    {
        if ((string) get_option(self::OPTION_SCHEMA_VERSION, '') !== self::SCHEMA_VERSION) {
            self::install_schema();
        }
    }

    private static function device_table()
    {
        global $wpdb;
        return $wpdb->prefix . 'karago_diafaneia_devices';
    }

    private static function request_table()
    {
        global $wpdb;
        return $wpdb->prefix . 'karago_diafaneia_requests';
    }

    private static function install_schema()
    {
        global $wpdb;
        require_once ABSPATH . 'wp-admin/includes/upgrade.php';
        $charset = $wpdb->get_charset_collate();
        $devices = self::device_table();
        $requests = self::request_table();

        dbDelta("CREATE TABLE {$devices} (
            id bigint(20) unsigned NOT NULL AUTO_INCREMENT,
            key_id varchar(24) NOT NULL,
            key_hash varchar(255) NOT NULL,
            key_hint varchar(64) NOT NULL,
            label varchar(100) NOT NULL,
            scope varchar(20) NOT NULL DEFAULT 'draft',
            author_user_id bigint(20) unsigned NOT NULL,
            created_at datetime NOT NULL,
            last_used_at datetime DEFAULT NULL,
            revoked_at datetime DEFAULT NULL,
            PRIMARY KEY  (id),
            UNIQUE KEY key_id (key_id),
            KEY revoked_at (revoked_at)
        ) {$charset};");

        dbDelta("CREATE TABLE {$requests} (
            request_id varchar(128) NOT NULL,
            payload_hash char(64) NOT NULL,
            state varchar(20) NOT NULL,
            post_id bigint(20) unsigned DEFAULT NULL,
            key_id varchar(24) NOT NULL,
            created_at datetime NOT NULL,
            updated_at datetime NOT NULL,
            last_error varchar(100) DEFAULT NULL,
            PRIMARY KEY  (request_id),
            KEY post_id (post_id),
            KEY updated_at (updated_at)
        ) {$charset};");

        update_option(self::OPTION_SCHEMA_VERSION, self::SCHEMA_VERSION, false);
    }

    public static function admin_menu()
    {
        add_options_page(
            'KARAGO Diafaneia Connector',
            'KARAGO Diafaneia',
            'manage_options',
            'karago-diafaneia-connector',
            array(__CLASS__, 'admin_page')
        );
    }

    public static function wrong_site_notice()
    {
        if (!current_user_can('manage_options') || self::is_correct_site()) {
            return;
        }
        echo '<div class="notice notice-error"><p><strong>KARAGO Diafaneia Publisher Connector:</strong> Το plugin είναι κλειδωμένο αποκλειστικά στο diafaneia.eu και δεν θα δημοσιεύσει σε αυτό το site.</p></div>';
    }

    public static function admin_page()
    {
        if (!current_user_can('manage_options')) {
            return;
        }

        global $wpdb;
        $devices = $wpdb->get_results('SELECT id, key_id, key_hint, label, scope, author_user_id, created_at, last_used_at, revoked_at FROM ' . self::device_table() . ' ORDER BY id DESC');
        $site_ok = self::is_correct_site();
        $https_ok = self::is_https_configured();
        ?>
        <div class="wrap" style="max-width:1050px">
            <h1>KARAGO Diafaneia Publisher Connector</h1>
            <p>Ένας Connector για Windows και Android, με ξεχωριστό ανακλητό API key για κάθε εξουσιοδοτημένη συσκευή.</p>

            <?php if (!$site_ok || !$https_ok) : ?>
                <div class="notice notice-error" style="padding:12px">
                    <p><strong>Ο Connector είναι κλειδωμένος.</strong></p>
                    <p>Απαιτείται ακριβώς το site <code>https://diafaneia.eu</code>. Τρέχον URL: <code><?php echo esc_html(home_url('/')); ?></code></p>
                </div>
            <?php endif; ?>

            <h2>Νέα εξουσιοδοτημένη συσκευή</h2>
            <form method="post" action="<?php echo esc_url(admin_url('admin-post.php')); ?>" style="background:#fff;border:1px solid #ccd0d4;padding:18px;max-width:950px">
                <input type="hidden" name="action" value="karago_diafaneia_create_device">
                <?php wp_nonce_field('karago_diafaneia_create_device'); ?>
                <table class="form-table" role="presentation">
                    <tr>
                        <th><label for="karago-device-label">Όνομα συσκευής</label></th>
                        <td><input id="karago-device-label" name="device_label" type="text" class="regular-text" maxlength="100" required placeholder="π.χ. PC γραφείου ή Κινητό Μαρίας"></td>
                    </tr>
                    <tr>
                        <th><label for="karago-device-scope">Δικαίωμα</label></th>
                        <td>
                            <select id="karago-device-scope" name="device_scope">
                                <option value="draft">Μόνο προσχέδια</option>
                                <option value="publish">Προσχέδια και δημοσίευση</option>
                            </select>
                        </td>
                    </tr>
                </table>
                <?php submit_button('Δημιουργία ξεχωριστού API key', 'primary', 'submit', false, (!$site_ok || !$https_ok) ? array('disabled' => 'disabled') : array()); ?>
            </form>

            <h2 style="margin-top:28px">Συσκευές και κλειδιά</h2>
            <table class="widefat striped" style="max-width:1050px">
                <thead><tr><th>Συσκευή</th><th>Κλειδί</th><th>Δικαίωμα</th><th>Συντάκτης</th><th>Δημιουργία</th><th>Τελευταία χρήση</th><th>Κατάσταση</th><th></th></tr></thead>
                <tbody>
                <?php if (!$devices) : ?>
                    <tr><td colspan="8">Δεν έχει δημιουργηθεί ακόμη κανένα κλειδί.</td></tr>
                <?php else : foreach ($devices as $device) : ?>
                    <tr>
                        <td><strong><?php echo esc_html($device->label); ?></strong></td>
                        <td><code><?php echo esc_html($device->key_hint); ?></code></td>
                        <td><?php echo $device->scope === 'publish' ? 'Προσχέδια + Δημοσίευση' : 'Μόνο προσχέδια'; ?></td>
                        <td><?php $author = get_userdata((int) $device->author_user_id); echo $author ? esc_html($author->display_name) : '—'; ?></td>
                        <td><?php echo esc_html($device->created_at); ?> UTC</td>
                        <td><?php echo $device->last_used_at ? esc_html($device->last_used_at) . ' UTC' : '—'; ?></td>
                        <td><?php echo $device->revoked_at ? '<span style="color:#a00">Ανακλήθηκε</span>' : '<span style="color:#087">Ενεργό</span>'; ?></td>
                        <td>
                            <?php if (!$device->revoked_at) : ?>
                            <form method="post" action="<?php echo esc_url(admin_url('admin-post.php')); ?>" onsubmit="return confirm('Να ανακληθεί μόνο αυτό το κλειδί;');">
                                <input type="hidden" name="action" value="karago_diafaneia_revoke_device">
                                <input type="hidden" name="device_id" value="<?php echo (int) $device->id; ?>">
                                <?php wp_nonce_field('karago_diafaneia_revoke_device_' . (int) $device->id); ?>
                                <button type="submit" class="button">Ανάκληση</button>
                            </form>
                            <?php endif; ?>
                        </td>
                    </tr>
                <?php endforeach; endif; ?>
                </tbody>
            </table>

            <h2 style="margin-top:28px">Ιδιωτικά endpoints</h2>
            <p><code>GET <?php echo esc_html(rest_url(self::REST_NAMESPACE . '/status')); ?></code></p>
            <p><code>GET <?php echo esc_html(rest_url(self::REST_NAMESPACE . '/terms')); ?></code></p>
            <p><code>POST <?php echo esc_html(rest_url(self::REST_NAMESPACE . '/publish')); ?></code></p>
            <p>Authentication header: <code><?php echo esc_html(self::HEADER_NAME); ?>: [το κλειδί της συγκεκριμένης συσκευής]</code></p>
            <p><strong>Δεν απαιτούνται και δεν χρησιμοποιούνται WordPress username, password ή Application Password.</strong></p>
        </div>
        <?php
    }

    public static function admin_create_device()
    {
        if (!current_user_can('manage_options')) {
            wp_die('Δεν επιτρέπεται.');
        }
        check_admin_referer('karago_diafaneia_create_device');
        if (!self::is_correct_site() || !self::is_https_configured()) {
            wp_die('Ο Connector δημιουργεί κλειδιά μόνο στο https://diafaneia.eu.');
        }

        $label = sanitize_text_field(isset($_POST['device_label']) ? wp_unslash($_POST['device_label']) : '');
        $scope = sanitize_key(isset($_POST['device_scope']) ? wp_unslash($_POST['device_scope']) : 'draft');
        if ($label === '' || self::string_length($label) > 100) {
            wp_die('Χρειάζεται έγκυρο όνομα συσκευής έως 100 χαρακτήρες.');
        }
        if (!in_array($scope, array('draft', 'publish'), true)) {
            wp_die('Άγνωστο δικαίωμα συσκευής.');
        }

        global $wpdb;
        $active = (int) $wpdb->get_var('SELECT COUNT(*) FROM ' . self::device_table() . ' WHERE revoked_at IS NULL');
        if ($active >= self::MAX_ACTIVE_KEYS) {
            wp_die('Έχει συμπληρωθεί το όριο ενεργών συσκευών. Ανακαλέστε ένα παλιό κλειδί και δοκιμάστε ξανά.');
        }

        do {
            $key_id = bin2hex(random_bytes(8));
            $exists = (int) $wpdb->get_var($wpdb->prepare('SELECT COUNT(*) FROM ' . self::device_table() . ' WHERE key_id = %s', $key_id));
        } while ($exists > 0);

        $secret = wp_generate_password(48, false, false);
        $raw_key = 'kdia_' . $key_id . '_' . $secret;
        $inserted = $wpdb->insert(
            self::device_table(),
            array(
                'key_id' => $key_id,
                'key_hash' => wp_hash_password($raw_key),
                'key_hint' => 'kdia_' . substr($key_id, 0, 8) . '…' . substr($secret, -6),
                'label' => $label,
                'scope' => $scope,
                'author_user_id' => get_current_user_id(),
                'created_at' => gmdate('Y-m-d H:i:s'),
            ),
            array('%s', '%s', '%s', '%s', '%s', '%d', '%s')
        );
        if (!$inserted) {
            wp_die('Δεν ήταν δυνατή η ασφαλής δημιουργία του κλειδιού.');
        }

        $settings_url = admin_url('options-general.php?page=karago-diafaneia-connector');
        $message = '<h1>Το κλειδί δημιουργήθηκε</h1>'
            . '<p>Συσκευή: <strong>' . esc_html($label) . '</strong></p>'
            . '<p><strong>Αντιγράψτε το τώρα. Στη βάση αποθηκεύτηκε μόνο το hash και το κλειδί δεν θα εμφανιστεί ξανά.</strong></p>'
            . '<p><input id="karago-diafaneia-new-key" type="text" readonly value="' . esc_attr($raw_key) . '" style="width:100%;max-width:900px;font-family:monospace;font-size:16px;padding:10px"></p>'
            . '<p><button type="button" class="button button-primary" onclick="navigator.clipboard.writeText(document.getElementById(\'karago-diafaneia-new-key\').value);this.textContent=\'Αντιγράφηκε\';">Αντιγραφή κλειδιού</button></p>'
            . '<p><a class="button" href="' . esc_url($settings_url) . '">Επιστροφή στις συσκευές</a></p>';
        wp_die($message, 'KARAGO Diafaneia Connector', array('response' => 200));
    }

    public static function admin_revoke_device()
    {
        if (!current_user_can('manage_options')) {
            wp_die('Δεν επιτρέπεται.');
        }
        $device_id = absint(isset($_POST['device_id']) ? $_POST['device_id'] : 0);
        check_admin_referer('karago_diafaneia_revoke_device_' . $device_id);
        if (!$device_id) {
            wp_die('Άγνωστη συσκευή.');
        }

        global $wpdb;
        $wpdb->query($wpdb->prepare(
            'UPDATE ' . self::device_table() . ' SET revoked_at = %s WHERE id = %d AND revoked_at IS NULL',
            gmdate('Y-m-d H:i:s'),
            $device_id
        ));
        wp_safe_redirect(admin_url('options-general.php?page=karago-diafaneia-connector&revoked=1'));
        exit;
    }

    public static function register_routes()
    {
        $route_options = array('permission_callback' => array(__CLASS__, 'authorize'));

        register_rest_route(self::REST_NAMESPACE, '/status', array_merge($route_options, array(
            'methods' => WP_REST_Server::READABLE,
            'callback' => array(__CLASS__, 'status'),
        )));
        register_rest_route(self::REST_NAMESPACE, '/terms', array_merge($route_options, array(
            'methods' => WP_REST_Server::READABLE,
            'callback' => array(__CLASS__, 'terms'),
        )));
        register_rest_route(self::REST_NAMESPACE, '/publish', array_merge($route_options, array(
            'methods' => WP_REST_Server::CREATABLE,
            'callback' => array(__CLASS__, 'publish'),
        )));
    }

    public static function authorize(WP_REST_Request $request)
    {
        self::$current_device = null;
        if (!self::is_https_configured()) {
            return self::error('karago_https_required', 'Ο Connector λειτουργεί μόνο μέσω HTTPS.', 403);
        }

        $provided = trim((string) $request->get_header('x-karago-key'));
        if ($provided === '' || strlen($provided) > 128 || !preg_match('/^kdia_([a-f0-9]{16})_([A-Za-z0-9]{48})$/D', $provided, $matches)) {
            return self::error('karago_unauthorized', 'Μη έγκυρο KARAGO API key.', 401);
        }

        global $wpdb;
        $device = $wpdb->get_row($wpdb->prepare(
            'SELECT id, key_id, key_hash, label, scope, author_user_id, last_used_at FROM ' . self::device_table() . ' WHERE key_id = %s AND revoked_at IS NULL LIMIT 1',
            $matches[1]
        ));
        if (!$device || !wp_check_password($provided, (string) $device->key_hash)) {
            return self::error('karago_unauthorized', 'Μη έγκυρο KARAGO API key.', 401);
        }

        $rate_error = self::check_rate_limit($request, $device->key_id);
        if (is_wp_error($rate_error)) {
            return $rate_error;
        }

        self::$current_device = $device;
        $now = gmdate('Y-m-d H:i:s');
        if (!$device->last_used_at || strtotime($device->last_used_at . ' UTC') < time() - 60) {
            $wpdb->update(self::device_table(), array('last_used_at' => $now), array('id' => (int) $device->id), array('%s'), array('%d'));
        }
        return true;
    }

    private static function check_rate_limit(WP_REST_Request $request, $key_id)
    {
        $is_publish = substr((string) $request->get_route(), -8) === '/publish';
        $limit = $is_publish ? 30 : 240;
        $window = 300;
        $transient_key = 'kdia_rl_' . sanitize_key($key_id) . ($is_publish ? '_p' : '_r');
        $state = get_transient($transient_key);
        if (!is_array($state) || empty($state['expires']) || (int) $state['expires'] <= time()) {
            $state = array('count' => 0, 'expires' => time() + $window);
        }
        if ((int) $state['count'] >= $limit) {
            return self::error('karago_rate_limited', 'Πάρα πολλά αιτήματα. Δοκιμάστε ξανά σε λίγο.', 429, array(
                'retry_after' => max(1, (int) $state['expires'] - time()),
            ));
        }
        $state['count'] = (int) $state['count'] + 1;
        set_transient($transient_key, $state, max(1, (int) $state['expires'] - time()));
        return true;
    }

    public static function status()
    {
        $site_error = self::site_error();
        if (is_wp_error($site_error)) {
            return $site_error;
        }
        $device = self::$current_device;
        $author = $device ? get_userdata((int) $device->author_user_id) : false;
        $can_edit = $author && user_can($author, 'edit_posts');
        $can_publish = $can_edit && $device->scope === 'publish' && user_can($author, 'publish_posts');
        return rest_ensure_response(array(
            'ready' => (bool) $can_edit,
            'version' => self::VERSION,
            'site' => self::SITE_KEY,
            'site_name' => get_bloginfo('name'),
            'site_url' => home_url('/'),
            'authentication' => 'karago_connector_api_key',
            'device' => array(
                'id' => $device ? (string) $device->key_id : '',
                'label' => $device ? (string) $device->label : '',
                'scope' => $device ? (string) $device->scope : '',
            ),
            'capabilities' => array(
                'draft' => (bool) $can_edit,
                'publish' => (bool) $can_publish,
                'terms' => true,
                'featured_image' => true,
                'inline_images' => true,
                'yoast_seo' => true,
                'request_id' => true,
                'multi_device_keys' => true,
            ),
        ));
    }

    public static function terms()
    {
        $site_error = self::site_error();
        if (is_wp_error($site_error)) {
            return $site_error;
        }

        $categories = get_terms(array(
            'taxonomy' => 'category',
            'hide_empty' => false,
            'orderby' => 'name',
            'order' => 'ASC',
        ));
        $tags = get_terms(array(
            'taxonomy' => 'post_tag',
            'hide_empty' => false,
            'orderby' => 'name',
            'order' => 'ASC',
        ));
        if (is_wp_error($categories) || is_wp_error($tags)) {
            return self::error('karago_terms_failed', 'Δεν ήταν δυνατή η ανάκτηση κατηγοριών και tags.', 500);
        }

        return rest_ensure_response(array(
            'site' => self::SITE_KEY,
            'categories' => array_values(array_map(function ($term) {
                return array(
                    'id' => (int) $term->term_id,
                    'name' => (string) $term->name,
                    'slug' => (string) $term->slug,
                    'parent' => (int) $term->parent,
                    'count' => (int) $term->count,
                );
            }, $categories)),
            'tags' => array_values(array_map(function ($term) {
                return array(
                    'id' => (int) $term->term_id,
                    'name' => (string) $term->name,
                    'slug' => (string) $term->slug,
                    'count' => (int) $term->count,
                );
            }, $tags)),
            'pagination' => false,
        ));
    }

    public static function publish(WP_REST_Request $request)
    {
        $site_error = self::site_error();
        if (is_wp_error($site_error)) {
            return $site_error;
        }

        $normalized = self::normalize_publish_request($request);
        if (is_wp_error($normalized)) {
            return $normalized;
        }
        $author = self::$current_device ? get_userdata((int) self::$current_device->author_user_id) : false;
        if (!$author || !user_can($author, 'edit_posts')) {
            return self::error('karago_author_unavailable', 'Ο WordPress συντάκτης αυτού του κλειδιού δεν έχει πλέον δικαίωμα επεξεργασίας.', 403);
        }
        if ($normalized['status'] === 'publish' && (self::$current_device->scope !== 'publish' || !user_can($author, 'publish_posts'))) {
            return self::error('karago_publish_scope_required', 'Το κλειδί αυτής της συσκευής επιτρέπει μόνο προσχέδια.', 403);
        }

        $payload_hash = self::payload_hash($normalized);
        $reservation = self::reserve_request($normalized['request_id'], $payload_hash, (string) self::$current_device->key_id);
        if (is_wp_error($reservation)) {
            return $reservation;
        }
        if (!empty($reservation['duplicate_post_id'])) {
            return self::post_response((int) $reservation['duplicate_post_id'], true, $normalized['request_id']);
        }

        $attachment_ids = array();
        $post_id = 0;
        try {
            $tags = self::resolve_tags($normalized['tag_names']);
            if (is_wp_error($tags)) {
                self::mark_request_failed($normalized['request_id'], $tags->get_error_code());
                return $tags;
            }

            $content = $normalized['content'];
            $featured_id = 0;
            if ($normalized['featured_image']) {
                $featured_id = self::upload_image($normalized['featured_image'], $normalized['title']);
                if (is_wp_error($featured_id)) {
                    self::mark_request_failed($normalized['request_id'], $featured_id->get_error_code());
                    return $featured_id;
                }
                $attachment_ids[] = (int) $featured_id;
            }

            foreach ($normalized['inline_images'] as $entry) {
                $inline_id = self::upload_image($entry['image'], $normalized['title']);
                if (is_wp_error($inline_id)) {
                    self::delete_attachments($attachment_ids);
                    self::mark_request_failed($normalized['request_id'], $inline_id->get_error_code());
                    return $inline_id;
                }
                $attachment_ids[] = (int) $inline_id;
                $html = wp_get_attachment_image((int) $inline_id, 'large', false, array(
                    'class' => 'karago-inline-image',
                    'loading' => 'lazy',
                ));
                if (!$html) {
                    self::delete_attachments($attachment_ids);
                    self::mark_request_failed($normalized['request_id'], 'karago_inline_image_html_failed');
                    return self::error('karago_inline_image_html_failed', 'Δεν ήταν δυνατή η δημιουργία του inline image.', 500);
                }
                $content = str_replace('{{KARAGO_INLINE_IMAGE:' . $entry['id'] . '}}', $html, $content);
            }

            if (preg_match('/\{\{KARAGO_INLINE_IMAGE:[^}]+\}\}/', $content)) {
                self::delete_attachments($attachment_ids);
                self::mark_request_failed($normalized['request_id'], 'karago_unresolved_inline_image');
                return self::error('karago_unresolved_inline_image', 'Υπάρχει inline image placeholder χωρίς αντίστοιχη εικόνα.', 400);
            }

            $post_data = array(
                'post_type' => 'post',
                'post_status' => $normalized['status'],
                'post_title' => $normalized['title'],
                'post_content' => $content,
                'post_excerpt' => $normalized['excerpt'],
                'post_name' => $normalized['slug'],
                'post_category' => $normalized['categories'],
                'post_author' => (int) self::$current_device->author_user_id,
                'meta_input' => array(
                    self::META_REQUEST_ID => $normalized['request_id'],
                    self::META_PAYLOAD_HASH => $payload_hash,
                    self::META_DEVICE_ID => (string) self::$current_device->key_id,
                    '_yoast_wpseo_title' => $normalized['seo_title'],
                    '_yoast_wpseo_metadesc' => $normalized['meta_description'],
                    '_yoast_wpseo_focuskw' => $normalized['focus_keyphrase'],
                ),
            );

            $post_id = wp_insert_post(wp_slash($post_data), true);
            if (is_wp_error($post_id)) {
                self::delete_attachments($attachment_ids);
                self::mark_request_failed($normalized['request_id'], $post_id->get_error_code());
                return $post_id;
            }
            $post_id = (int) $post_id;

            $tag_result = wp_set_post_terms($post_id, $tags['ids'], 'post_tag', false);
            if (is_wp_error($tag_result)) {
                wp_delete_post($post_id, true);
                self::delete_attachments($attachment_ids);
                self::mark_request_failed($normalized['request_id'], $tag_result->get_error_code());
                return $tag_result;
            }
            if ($featured_id && !set_post_thumbnail($post_id, (int) $featured_id)) {
                wp_delete_post($post_id, true);
                self::delete_attachments($attachment_ids);
                self::mark_request_failed($normalized['request_id'], 'karago_featured_image_failed');
                return self::error('karago_featured_image_failed', 'Δεν ήταν δυνατός ο ορισμός της featured image.', 500);
            }

            clean_post_cache($post_id);
            self::mark_request_completed($normalized['request_id'], $post_id);
            return self::post_response($post_id, false, $normalized['request_id'], $tags['resolved']);
        } catch (Throwable $exception) {
            if ($post_id) {
                wp_delete_post($post_id, true);
            }
            self::delete_attachments($attachment_ids);
            self::mark_request_failed($normalized['request_id'], 'karago_internal_error');
            return self::error('karago_internal_error', 'Η δημοσίευση δεν ολοκληρώθηκε. Είναι ασφαλές να ξαναδοκιμάσετε με το ίδιο REQUEST_ID.', 500);
        }
    }

    private static function normalize_publish_request(WP_REST_Request $request)
    {
        $site = sanitize_key(self::request_string($request, 'site'));
        if ($site !== self::SITE_KEY) {
            return self::error('karago_invalid_site', 'Το SITE πρέπει να είναι diafaneia.', 400);
        }

        $request_id = strtolower(trim(self::request_string($request, 'request_id')));
        if (!preg_match('/^[a-z0-9][a-z0-9._:-]{5,127}$/D', $request_id)) {
            return self::error('karago_invalid_request_id', 'Απαιτείται έγκυρο REQUEST_ID από 6 έως 128 χαρακτήρες.', 400);
        }

        $status = sanitize_key(self::request_string($request, 'status'));
        if (!in_array($status, array('draft', 'publish'), true)) {
            return self::error('karago_invalid_status', 'Το status πρέπει να είναι draft ή publish.', 400);
        }

        $title = sanitize_text_field(self::request_string($request, 'title'));
        $content_raw = self::request_string($request, 'content');
        $content = self::sanitize_content($content_raw);
        $excerpt = sanitize_textarea_field(self::request_string($request, 'excerpt'));
        $slug = sanitize_title(self::request_string($request, 'slug'));
        $seo_plugin = sanitize_key(self::request_string($request, 'seo_plugin', 'yoast'));
        $seo_title = sanitize_text_field(self::request_string($request, 'seo_title'));
        $meta_description = sanitize_textarea_field(self::request_string($request, 'meta_description'));
        $focus_keyphrase = sanitize_text_field(self::request_string($request, 'focus_keyphrase'));

        if ($title === '' || trim(wp_strip_all_tags($content)) === '' || $excerpt === '' || $slug === '' || $seo_title === '' || $meta_description === '' || $focus_keyphrase === '') {
            return self::error('karago_missing_required', 'Απαιτούνται title, content, excerpt, slug και όλα τα πεδία Yoast SEO.', 400);
        }
        if ($seo_plugin !== 'yoast') {
            return self::error('karago_invalid_seo_plugin', 'Η Διαφάνεια χρησιμοποιεί αποκλειστικά Yoast SEO.', 400);
        }
        if (self::string_length($title) > 500 || self::string_length($excerpt) > 10000 || self::string_length($seo_title) > 500 || self::string_length($meta_description) > 5000 || self::string_length($focus_keyphrase) > 500 || strlen($content_raw) > 2097152) {
            return self::error('karago_payload_too_large', 'Ένα ή περισσότερα πεδία υπερβαίνουν το επιτρεπόμενο μέγεθος.', 413);
        }
        if (trim(self::request_string($request, 'team_tag')) !== '') {
            return self::error('karago_team_tag_not_supported', 'Το TEAM_TAG δεν χρησιμοποιείται στη Διαφάνεια.', 400);
        }

        $categories = self::resolve_categories($request->get_param('categories'));
        if (is_wp_error($categories)) {
            return $categories;
        }
        $tag_names = self::normalize_tag_names($request->get_param('tag_names'));
        if (is_wp_error($tag_names)) {
            return $tag_names;
        }

        $featured = self::normalize_image_descriptor($request->get_param('featured_image'), false);
        if (is_wp_error($featured)) {
            return $featured;
        }
        $inline = self::normalize_inline_images($request->get_param('inline_images'), $content);
        if (is_wp_error($inline)) {
            return $inline;
        }

        $total_image_bytes = $featured ? self::estimated_decoded_bytes($featured['data_base64']) : 0;
        foreach ($inline as $entry) {
            $total_image_bytes += self::estimated_decoded_bytes($entry['image']['data_base64']);
        }
        if ($total_image_bytes > self::MAX_TOTAL_IMAGE_BYTES) {
            return self::error('karago_images_too_large', 'Το συνολικό μέγεθος εικόνων υπερβαίνει τα 50 MB.', 413);
        }

        return array(
            'site' => self::SITE_KEY,
            'request_id' => $request_id,
            'status' => $status,
            'title' => $title,
            'content' => $content,
            'excerpt' => $excerpt,
            'slug' => $slug,
            'categories' => $categories,
            'tag_names' => $tag_names,
            'featured_image' => $featured,
            'inline_images' => $inline,
            'seo_plugin' => 'yoast',
            'seo_title' => $seo_title,
            'meta_description' => $meta_description,
            'focus_keyphrase' => $focus_keyphrase,
        );
    }

    private static function resolve_categories($value)
    {
        if (!is_array($value) || !$value || count($value) > 20) {
            return self::error('karago_invalid_categories', 'Απαιτείται λίστα από 1 έως 20 πραγματικά category IDs.', 400);
        }
        $ids = array();
        $unknown = array();
        foreach ($value as $raw) {
            if (!is_numeric($raw) || (int) $raw <= 0) {
                $unknown[] = is_scalar($raw) ? (string) $raw : '?';
                continue;
            }
            $id = (int) $raw;
            $term = get_term($id, 'category');
            if (!$term || is_wp_error($term) || $term->taxonomy !== 'category') {
                $unknown[] = (string) $id;
            } else {
                $ids[] = $id;
            }
        }
        $ids = array_values(array_unique($ids));
        sort($ids, SORT_NUMERIC);
        if ($unknown) {
            return self::error('karago_unknown_category', 'Άγνωστα category IDs: ' . implode(', ', array_slice($unknown, 0, 20)) . '.', 400);
        }
        if (!$ids) {
            return self::error('karago_missing_category', 'Απαιτείται τουλάχιστον μία κατηγορία.', 400);
        }
        return $ids;
    }

    private static function normalize_tag_names($value)
    {
        if (!is_array($value) || !$value || count($value) > 50) {
            return self::error('karago_invalid_tags', 'Απαιτείται λίστα από 1 έως 50 tag names.', 400);
        }
        $tags = array();
        $seen = array();
        foreach ($value as $raw) {
            if (!is_scalar($raw)) {
                return self::error('karago_invalid_tags', 'Τα tags πρέπει να είναι απλό κείμενο.', 400);
            }
            $name = trim(sanitize_text_field((string) $raw));
            if ($name === '') {
                continue;
            }
            if (self::string_length($name) > 200) {
                return self::error('karago_invalid_tags', 'Ένα tag υπερβαίνει τους 200 χαρακτήρες.', 400);
            }
            $key = self::normalize_term($name);
            if ($key !== '' && !isset($seen[$key])) {
                $seen[$key] = true;
                $tags[] = $name;
            }
        }
        if (!$tags) {
            return self::error('karago_missing_tags', 'Απαιτείται τουλάχιστον ένα tag.', 400);
        }
        return $tags;
    }

    private static function normalize_image_descriptor($value, $required)
    {
        if ($value === null || $value === '') {
            return $required ? self::error('karago_missing_image', 'Λείπει εικόνα.', 400) : null;
        }
        if (!is_array($value)) {
            return self::error('karago_bad_image', 'Η εικόνα έχει άγνωστη μορφή.', 400);
        }
        $data = isset($value['data_base64']) && is_string($value['data_base64']) ? trim($value['data_base64']) : '';
        $mime = isset($value['mime_type']) && is_scalar($value['mime_type']) ? sanitize_mime_type((string) $value['mime_type']) : '';
        $name = isset($value['name']) && is_scalar($value['name']) ? sanitize_file_name((string) $value['name']) : '';
        $alt = isset($value['alt_text']) && is_scalar($value['alt_text']) ? sanitize_text_field((string) $value['alt_text']) : '';
        $allowed = array('image/jpeg', 'image/png', 'image/webp', 'image/gif');
        if ($data === '' || !in_array($mime, $allowed, true)) {
            return self::error('karago_bad_image', 'Η εικόνα πρέπει να είναι έγκυρο JPEG, PNG, WebP ή GIF.', 400);
        }
        if (strlen($data) > (int) ceil(self::MAX_IMAGE_BYTES / 3) * 4 + 8 || self::estimated_decoded_bytes($data) > self::MAX_IMAGE_BYTES) {
            return self::error('karago_image_too_large', 'Κάθε εικόνα μπορεί να είναι έως 20 MB.', 413);
        }
        if ($name === '') {
            $name = 'karago-image.' . self::extension_for_mime($mime);
        }
        return array('name' => $name, 'mime_type' => $mime, 'data_base64' => $data, 'alt_text' => $alt);
    }

    private static function normalize_inline_images($value, $content)
    {
        if ($value === null || $value === '') {
            $value = array();
        }
        if (!is_array($value) || count($value) > self::MAX_INLINE_IMAGES) {
            return self::error('karago_invalid_inline_images', 'Επιτρέπονται έως 20 inline images.', 400);
        }
        $result = array();
        $seen = array();
        foreach ($value as $entry) {
            if (!is_array($entry)) {
                return self::error('karago_invalid_inline_images', 'Άγνωστη μορφή inline image.', 400);
            }
            $id = isset($entry['id']) && is_scalar($entry['id']) ? (string) $entry['id'] : '';
            if (!preg_match('/^[A-Za-z0-9_-]{1,64}$/D', $id) || isset($seen[$id])) {
                return self::error('karago_invalid_inline_image_id', 'Κάθε inline image χρειάζεται μοναδικό ασφαλές id.', 400);
            }
            if (strpos($content, '{{KARAGO_INLINE_IMAGE:' . $id . '}}') === false) {
                return self::error('karago_unused_inline_image', 'Inline image χωρίς αντίστοιχο placeholder: ' . $id . '.', 400);
            }
            $image = self::normalize_image_descriptor(isset($entry['image']) ? $entry['image'] : null, true);
            if (is_wp_error($image)) {
                return $image;
            }
            $seen[$id] = true;
            $result[] = array('id' => $id, 'image' => $image);
        }

        if (preg_match_all('/\{\{KARAGO_INLINE_IMAGE:([^}]+)\}\}/', $content, $matches)) {
            foreach (array_unique($matches[1]) as $placeholder_id) {
                if (!isset($seen[$placeholder_id])) {
                    return self::error('karago_missing_inline_image', 'Λείπει η εικόνα για το placeholder: ' . sanitize_text_field($placeholder_id) . '.', 400);
                }
            }
        }
        return $result;
    }

    private static function payload_hash($payload)
    {
        $canonical = $payload;
        $tags = $canonical['tag_names'];
        usort($tags, function ($a, $b) {
            return strcmp(self::normalize_term($a), self::normalize_term($b));
        });
        $canonical['tag_names'] = $tags;
        usort($canonical['inline_images'], function ($a, $b) {
            return strcmp($a['id'], $b['id']);
        });
        return hash('sha256', wp_json_encode($canonical, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE));
    }

    private static function reserve_request($request_id, $payload_hash, $key_id)
    {
        global $wpdb;
        $table = self::request_table();
        $now = gmdate('Y-m-d H:i:s');
        $inserted = $wpdb->query($wpdb->prepare(
            "INSERT IGNORE INTO {$table} (request_id, payload_hash, state, post_id, key_id, created_at, updated_at, last_error) VALUES (%s, %s, 'processing', NULL, %s, %s, %s, NULL)",
            $request_id,
            $payload_hash,
            $key_id,
            $now,
            $now
        ));
        if ($inserted === 1) {
            return array('acquired' => true);
        }

        $row = $wpdb->get_row($wpdb->prepare('SELECT request_id, payload_hash, state, post_id, updated_at FROM ' . $table . ' WHERE request_id = %s LIMIT 1', $request_id));
        if (!$row) {
            return self::error('karago_idempotency_unavailable', 'Δεν ήταν δυνατή η ασφαλής δέσμευση του REQUEST_ID.', 503);
        }
        if (!hash_equals((string) $row->payload_hash, $payload_hash)) {
            return self::error('karago_request_id_conflict', 'Το ίδιο REQUEST_ID χρησιμοποιήθηκε με διαφορετικό περιεχόμενο.', 409);
        }

        if ($row->state === 'completed' && (int) $row->post_id > 0 && get_post((int) $row->post_id)) {
            return array('duplicate_post_id' => (int) $row->post_id);
        }
        if ($row->state === 'completed') {
            $existing = self::find_post_by_request_id($request_id);
            if ($existing) {
                self::mark_request_completed($request_id, $existing);
                return array('duplicate_post_id' => $existing);
            }
        }

        if ($row->state === 'failed') {
            $existing = self::find_post_by_request_id($request_id);
            if ($existing) {
                self::mark_request_completed($request_id, $existing);
                return array('duplicate_post_id' => $existing);
            }
            $updated = $wpdb->query($wpdb->prepare(
                "UPDATE {$table} SET state = 'processing', key_id = %s, updated_at = %s, last_error = NULL WHERE request_id = %s AND payload_hash = %s AND state = 'failed'",
                $key_id,
                $now,
                $request_id,
                $payload_hash
            ));
            if ($updated === 1) {
                return array('acquired' => true);
            }
        }

        $cutoff = gmdate('Y-m-d H:i:s', time() - self::PROCESSING_STALE_SECONDS);
        if ($row->state === 'processing' && (string) $row->updated_at < $cutoff) {
            $existing = self::find_post_by_request_id($request_id);
            if ($existing) {
                self::mark_request_completed($request_id, $existing);
                return array('duplicate_post_id' => $existing);
            }
            $updated = $wpdb->query($wpdb->prepare(
                "UPDATE {$table} SET key_id = %s, updated_at = %s, last_error = NULL WHERE request_id = %s AND payload_hash = %s AND state = 'processing' AND updated_at < %s",
                $key_id,
                $now,
                $request_id,
                $payload_hash,
                $cutoff
            ));
            if ($updated === 1) {
                return array('acquired' => true);
            }
        }

        return self::error('karago_request_in_progress', 'Το ίδιο REQUEST_ID επεξεργάζεται ήδη. Περιμένετε λίγο και ξαναδοκιμάστε με το ίδιο REQUEST_ID.', 409, array('retry_after' => 5));
    }

    private static function find_post_by_request_id($request_id)
    {
        $ids = get_posts(array(
            'post_type' => 'post',
            'post_status' => array('draft', 'pending', 'publish', 'future', 'private'),
            'posts_per_page' => 1,
            'fields' => 'ids',
            'meta_key' => self::META_REQUEST_ID,
            'meta_value' => $request_id,
            'orderby' => 'ID',
            'order' => 'DESC',
            'no_found_rows' => true,
        ));
        return $ids ? (int) $ids[0] : 0;
    }

    private static function mark_request_completed($request_id, $post_id)
    {
        global $wpdb;
        $wpdb->query($wpdb->prepare(
            "UPDATE " . self::request_table() . " SET state = 'completed', post_id = %d, updated_at = %s, last_error = NULL WHERE request_id = %s",
            (int) $post_id,
            gmdate('Y-m-d H:i:s'),
            $request_id
        ));
    }

    private static function mark_request_failed($request_id, $error_code)
    {
        global $wpdb;
        $wpdb->update(
            self::request_table(),
            array('state' => 'failed', 'updated_at' => gmdate('Y-m-d H:i:s'), 'last_error' => substr(sanitize_key($error_code), 0, 100)),
            array('request_id' => $request_id),
            array('%s', '%s', '%s'),
            array('%s')
        );
    }

    private static function resolve_tags($names)
    {
        $existing = get_terms(array('taxonomy' => 'post_tag', 'hide_empty' => false));
        if (is_wp_error($existing)) {
            return self::error('karago_tags_failed', 'Δεν ήταν δυνατή η ανάκτηση των tags.', 500);
        }
        $map = array();
        foreach ($existing as $term) {
            $key = self::normalize_term($term->name);
            if ($key !== '' && !isset($map[$key])) {
                $map[$key] = $term;
            }
        }

        $ids = array();
        $resolved = array();
        foreach ($names as $name) {
            $key = self::normalize_term($name);
            $term = isset($map[$key]) ? $map[$key] : null;
            if (!$term) {
                $created = wp_insert_term($name, 'post_tag');
                if (is_wp_error($created)) {
                    if ($created->get_error_code() !== 'term_exists') {
                        return $created;
                    }
                    $term_id = (int) $created->get_error_data();
                } else {
                    $term_id = (int) $created['term_id'];
                }
                $term = get_term($term_id, 'post_tag');
                if (!$term || is_wp_error($term)) {
                    return self::error('karago_tag_resolution_failed', 'Δεν ήταν δυνατή η επίλυση ενός tag.', 500);
                }
                $map[$key] = $term;
            }
            $ids[] = (int) $term->term_id;
            $resolved[] = array('requested' => $name, 'used' => (string) $term->name, 'id' => (int) $term->term_id);
        }
        return array('ids' => array_values(array_unique($ids)), 'resolved' => $resolved);
    }

    private static function upload_image($image, $title)
    {
        $binary = base64_decode($image['data_base64'], true);
        if ($binary === false || strlen($binary) === 0 || strlen($binary) > self::MAX_IMAGE_BYTES) {
            return self::error('karago_bad_image', 'Η εικόνα δεν είναι έγκυρη ή υπερβαίνει τα 20 MB.', 400);
        }

        $filename = sanitize_file_name($image['name']);
        if ($filename === '') {
            $filename = 'karago-image.' . self::extension_for_mime($image['mime_type']);
        }
        $temporary = wp_tempnam($filename);
        if (!$temporary || file_put_contents($temporary, $binary) !== strlen($binary)) {
            if ($temporary && file_exists($temporary)) {
                @unlink($temporary);
            }
            return self::error('karago_image_temp_failed', 'Δεν ήταν δυνατή η ασφαλής προετοιμασία της εικόνας.', 500);
        }
        unset($binary);

        require_once ABSPATH . 'wp-admin/includes/file.php';
        require_once ABSPATH . 'wp-admin/includes/media.php';
        require_once ABSPATH . 'wp-admin/includes/image.php';
        $allowed_mimes = array(
            'jpg|jpeg|jpe' => 'image/jpeg',
            'png' => 'image/png',
            'webp' => 'image/webp',
            'gif' => 'image/gif',
        );
        $checked = wp_check_filetype_and_ext($temporary, $filename, $allowed_mimes);
        $image_info = wp_getimagesize($temporary);
        $actual_mime = is_array($image_info) && isset($image_info['mime']) ? (string) $image_info['mime'] : '';
        if (empty($checked['type']) || empty($checked['ext']) || !in_array($checked['type'], $allowed_mimes, true) || $checked['type'] !== $image['mime_type'] || $actual_mime !== $checked['type']) {
            @unlink($temporary);
            return self::error('karago_bad_image_type', 'Το πραγματικό αρχείο εικόνας δεν συμφωνεί με τον δηλωμένο τύπο.', 400);
        }

        $safe_filename = sanitize_file_name(pathinfo($filename, PATHINFO_FILENAME) . '.' . $checked['ext']);
        $file_array = array('name' => $safe_filename, 'tmp_name' => $temporary);
        $attachment_id = media_handle_sideload($file_array, 0, sanitize_text_field($title));
        if (is_wp_error($attachment_id)) {
            if (file_exists($temporary)) {
                @unlink($temporary);
            }
            return self::error('karago_upload_failed', 'Δεν ήταν δυνατή η αποθήκευση της εικόνας.', 500);
        }
        if (!empty($image['alt_text'])) {
            update_post_meta((int) $attachment_id, '_wp_attachment_image_alt', sanitize_text_field($image['alt_text']));
        }
        return (int) $attachment_id;
    }

    private static function delete_attachments($ids)
    {
        foreach (array_unique(array_map('absint', $ids)) as $id) {
            if ($id) {
                wp_delete_attachment($id, true);
            }
        }
    }

    private static function post_response($post_id, $duplicate, $request_id, $resolved_tags = array())
    {
        $post = get_post($post_id);
        if (!$post) {
            return self::error('karago_post_not_found', 'Το αποθηκευμένο άρθρο δεν βρέθηκε.', 500);
        }
        $url = get_permalink($post_id);
        return rest_ensure_response(array(
            'ok' => true,
            'duplicate' => (bool) $duplicate,
            'request_id' => (string) $request_id,
            'post_id' => (int) $post_id,
            'status' => (string) $post->post_status,
            'title' => get_the_title($post_id),
            'url' => $url,
            'link' => $url,
            'edit_link' => get_edit_post_link($post_id, 'raw'),
            'site' => self::SITE_KEY,
            'resolved_tags' => $resolved_tags,
        ));
    }

    private static function sanitize_content($content)
    {
        $allowed = wp_kses_allowed_html('post');
        $allowed['iframe'] = array(
            'src' => true,
            'width' => true,
            'height' => true,
            'title' => true,
            'frameborder' => true,
            'allow' => true,
            'allowfullscreen' => true,
            'loading' => true,
            'referrerpolicy' => true,
            'class' => true,
            'style' => true,
        );
        $allowed['ins'] = array(
            'class' => true,
            'style' => true,
            'id' => true,
            'data-ad-layout' => true,
            'data-ad-format' => true,
            'data-ad-client' => true,
            'data-ad-slot' => true,
            'data-full-width-responsive' => true,
        );
        foreach (array('div', 'blockquote', 'a', 'img', 'p', 'span') as $tag) {
            if (!isset($allowed[$tag])) {
                $allowed[$tag] = array();
            }
            $allowed[$tag]['data-href'] = true;
            $allowed[$tag]['data-width'] = true;
            $allowed[$tag]['data-show-text'] = true;
            $allowed[$tag]['data-instgrm-permalink'] = true;
            $allowed[$tag]['data-instgrm-version'] = true;
            $allowed[$tag]['data-tiktok-embed'] = true;
            $allowed[$tag]['cite'] = true;
        }
        return wp_kses((string) $content, $allowed, array('https'));
    }

    private static function normalize_term($value)
    {
        $value = remove_accents(wp_strip_all_tags((string) $value));
        $value = function_exists('mb_strtolower') ? mb_strtolower($value, 'UTF-8') : strtolower($value);
        $value = preg_replace('/[\.·\'’`΄"“”()\[\]{}\-_]+/u', ' ', $value);
        $value = preg_replace('/[^a-z0-9α-ωϊϋΐΰς\s]/iu', ' ', $value);
        return trim(preg_replace('/\s+/u', ' ', $value));
    }

    private static function request_string(WP_REST_Request $request, $key, $default = '')
    {
        $value = $request->get_param($key);
        if ($value === null) {
            return $default;
        }
        return is_scalar($value) ? (string) $value : $default;
    }

    private static function string_length($value)
    {
        return function_exists('mb_strlen') ? mb_strlen((string) $value, 'UTF-8') : strlen((string) $value);
    }

    private static function estimated_decoded_bytes($base64)
    {
        $length = strlen((string) $base64);
        $padding = $length >= 2 ? substr_count(substr((string) $base64, -2), '=') : 0;
        return (int) floor($length * 3 / 4) - $padding;
    }

    private static function extension_for_mime($mime)
    {
        $map = array('image/jpeg' => 'jpg', 'image/png' => 'png', 'image/webp' => 'webp', 'image/gif' => 'gif');
        return isset($map[$mime]) ? $map[$mime] : 'jpg';
    }

    private static function site_error()
    {
        if (!self::is_correct_site()) {
            return self::error('karago_wrong_site', 'Ο Connector είναι κλειδωμένος αποκλειστικά στο diafaneia.eu.', 409, array(
                'expected_site' => self::SITE_KEY,
            ));
        }
        return true;
    }

    private static function is_correct_site()
    {
        $host = strtolower((string) wp_parse_url(home_url('/'), PHP_URL_HOST));
        if (strpos($host, 'www.') === 0) {
            $host = substr($host, 4);
        }
        return $host === self::EXPECTED_HOST;
    }

    private static function is_https_configured()
    {
        return strtolower((string) wp_parse_url(home_url('/'), PHP_URL_SCHEME)) === 'https';
    }

    private static function error($code, $message, $status, $extra = array())
    {
        return new WP_Error($code, $message, array_merge(array('status' => (int) $status), $extra));
    }
}

register_activation_hook(__FILE__, array('KARAGO_Diafaneia_Publisher_Connector', 'activate'));
KARAGO_Diafaneia_Publisher_Connector::init();
