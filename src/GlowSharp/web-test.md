# Web Languages Test

Testing syntax highlighting for HTML, XML, and CSS.

## HTML Example

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Hello World</title>
    <link rel="stylesheet" href="styles.css">
</head>
<body>
    <!-- This is a comment -->
    <div class="container" id="main">
        <h1>Welcome to GlowSharp!</h1>
        <p>This is a <strong>bold</strong> paragraph with <em>italic</em> text.</p>
        <img src="logo.png" alt="Logo" />
        <a href="https://example.com" target="_blank">Click here</a>
    </div>
    <script src="app.js"></script>
</body>
</html>
```

## XML Example

```xml
<?xml version="1.0" encoding="UTF-8"?>
<bookstore>
    <!-- Book catalog -->
    <book category="programming" isbn="978-0-13-468599-1">
        <title lang="en">The C Programming Language</title>
        <author>Brian Kernighan</author>
        <author>Dennis Ritchie</author>
        <year>1978</year>
        <price currency="USD">49.99</price>
        <description><![CDATA[A classic book about C programming.]]></description>
    </book>
    <book category="web" isbn="978-1-491-91866-1">
        <title lang="en">Learning Web Design</title>
        <author>Jennifer Niederst Robbins</author>
        <year>2018</year>
        <price currency="USD">39.99</price>
    </book>
</bookstore>
```

## CSS Example

```css
/* Global styles */
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: 'Arial', sans-serif;
    font-size: 16px;
    line-height: 1.6;
    color: #333;
    background-color: #f4f4f4;
}

/* Container */
.container {
    width: 90%;
    max-width: 1200px;
    margin: 0 auto;
    padding: 20px;
}

#main {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    border-radius: 8px;
    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
}

/* Typography */
h1, h2, h3 {
    font-weight: bold;
    margin-bottom: 1rem;
    color: #2c3e50;
}

h1 {
    font-size: 2.5rem;
}

/* Links */
a {
    color: #3498db;
    text-decoration: none;
    transition: color 0.3s ease;
}

a:hover {
    color: #2980b9 !important;
}

/* Media queries */
@media (max-width: 768px) {
    .container {
        width: 95%;
        padding: 10px;
    }
    
    h1 {
        font-size: 2rem;
    }
}
```

All three web languages are now supported! 🎨
