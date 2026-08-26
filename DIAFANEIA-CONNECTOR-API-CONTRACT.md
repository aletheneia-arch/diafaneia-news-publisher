# KARAGO Diafaneia Publisher Connector 1.0.0 — API contract

Το contract είναι κοινό για Windows και Android. Κάθε εγκατάσταση εφαρμογής χρησιμοποιεί δικό της, ανακλητό API key.

## Base URL και authentication

- Base URL: `https://diafaneia.eu`
- Header: `X-KARAGO-Key: <device-specific-key>`
- Redirects: ο client δεν πρέπει να ακολουθεί redirects όταν έχει ήδη προσθέσει το ιδιωτικό header.
- Δεν χρησιμοποιούνται WordPress username, password ή Application Password.

## GET `/wp-json/karago-diafaneia/v1/status`

Επιστρέφει `ready`, `version`, `site`, `site_name`, `site_url`, τα μη απόρρητα στοιχεία της συσκευής και capabilities (`draft`, `publish`, `terms`, images, Yoast, REQUEST_ID, multi-device keys).

Ο client επιβεβαιώνει υποχρεωτικά `ready=true`, `site=diafaneia` και host `diafaneia.eu` πριν θεωρήσει τη σύνδεση έγκυρη.

## GET `/wp-json/karago-diafaneia/v1/terms`

Δεν έχει pagination και επιστρέφει όλες τις πραγματικές τιμές:

```json
{
  "site": "diafaneia",
  "categories": [
    {"id": 12, "name": "Νέα", "slug": "nea", "parent": 0, "count": 100}
  ],
  "tags": [
    {"id": 34, "name": "Χίος", "slug": "chios", "count": 50}
  ],
  "pagination": false
}
```

## POST `/wp-json/karago-diafaneia/v1/publish`

```json
{
  "site": "diafaneia",
  "request_id": "σταθερό-μοναδικό-id",
  "status": "draft",
  "title": "Τίτλος",
  "content": "<p>Πλήρες HTML</p>",
  "excerpt": "Περίληψη",
  "slug": "slug",
  "categories": [12, 15],
  "tag_names": ["Χίος", "Εκπαίδευση"],
  "featured_image": {
    "name": "photo.jpg",
    "mime_type": "image/jpeg",
    "data_base64": "...",
    "alt_text": "Περιγραφή"
  },
  "inline_images": [
    {
      "id": "photo_1",
      "image": {
        "name": "inline.jpg",
        "mime_type": "image/jpeg",
        "data_base64": "...",
        "alt_text": "Περιγραφή"
      }
    }
  ],
  "seo_plugin": "yoast",
  "seo_title": "SEO title",
  "meta_description": "Meta description",
  "focus_keyphrase": "focus keyphrase"
}
```

- `status`: μόνο `draft` ή `publish`. Άγνωστη τιμή απορρίπτεται· δεν αλλάζει σιωπηρά.
- `categories`: υπάρχοντα WordPress category IDs. Άγνωστα IDs απορρίπτονται.
- `tag_names`: πραγματικά ονόματα. Υπάρχοντα tags επαναχρησιμοποιούνται και νέα δημιουργούνται.
- `featured_image`: προαιρετικό.
- `inline_images`: προαιρετικός πίνακας έως 20 εικόνες. Κάθε id αντιστοιχεί σε placeholder `{{KARAGO_INLINE_IMAGE:id}}` μέσα στο content.
- Εικόνες: JPEG/PNG/WebP/GIF, έως 20 MB η καθεμία και 50 MB συνολικά. Ελέγχεται το πραγματικό αρχείο, όχι μόνο το δηλωμένο MIME.
- Yoast: `seo_title` → `_yoast_wpseo_title`, `meta_description` → `_yoast_wpseo_metadesc`, `focus_keyphrase` → `_yoast_wpseo_focuskw`.

Επιτυχής απάντηση:

```json
{
  "ok": true,
  "duplicate": false,
  "request_id": "σταθερό-μοναδικό-id",
  "post_id": 123,
  "status": "draft",
  "title": "Τίτλος",
  "url": "https://diafaneia.eu/?p=123",
  "link": "https://diafaneia.eu/?p=123",
  "edit_link": "https://diafaneia.eu/wp-admin/post.php?post=123&action=edit",
  "site": "diafaneia",
  "resolved_tags": []
}
```

## REQUEST_ID / idempotency

- Το `request_id` είναι υποχρεωτικό και ισχύει συνολικά για όλες τις συσκευές.
- Η πρώτη κλήση δεσμεύει ατομικά το REQUEST_ID πριν από οποιαδήποτε δημιουργία άρθρου.
- Ίδιο REQUEST_ID και ίδιο payload επιστρέφει το ήδη δημιουργημένο post με `duplicate=true`.
- Ίδιο REQUEST_ID και διαφορετικό payload επιστρέφει HTTP `409`.
- Ταυτόχρονη δεύτερη κλήση επιστρέφει HTTP `409` με `karago_request_in_progress` και δεν δημιουργεί δεύτερο άρθρο.
- Μετά από timeout, ο client επαναχρησιμοποιεί ακριβώς το ίδιο REQUEST_ID και payload.

## HTTP errors

- `400`: άκυρα/ελλιπή δεδομένα.
- `401`: λείπει, είναι λάθος ή έχει ανακληθεί το API key.
- `403`: HTTPS/security failure ή ανεπαρκές scope για Publish.
- `404`: το plugin/route δεν είναι διαθέσιμο.
- `409`: wrong-site, REQUEST_ID conflict ή αίτημα ήδη σε εξέλιξη.
- `413`: payload/image πολύ μεγάλο.
- `429`: rate limit, με `retry_after` στα error data.
- `5xx`: προσωρινό ή εσωτερικό σφάλμα. Το API key δεν περιλαμβάνεται ποτέ στο μήνυμα.

