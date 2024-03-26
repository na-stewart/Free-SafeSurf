<!-- Improved compatibility of back to top link: See: https://github.com/othneildrew/Best-README-Template/pull/73 -->
<a name="readme-top"></a>
<!--
*** Thanks for checking out the Best-README-Template. If you have a suggestion
*** that would make this better, please fork the repo and create a pull request
*** or simply open an issue with the tag "enhancement".
*** Don't forget to give the project a star!
*** Thanks again! Now go create something AMAZING! :D
-->



<!-- PROJECT SHIELDS -->
<!--
*** I'm using markdown "reference style" links for readability.
*** Reference links are enclosed in brackets [ ] instead of parentheses ( ).
*** See the bottom of this document for the declaration of the reference variables
*** for contributors-url, forks-url, etc. This is an optional, concise syntax you may use.
*** https://www.markdownguide.org/basic-syntax/#reference-style-links
-->


<!-- PROJECT LOGO -->
<br />
<div align="center">
  <a href="https://github.com/othneildrew/Best-README-Template">
    <img src="https://github.com/na-stewart/SafeSurf/blob/master/img/safe-surf.png" alt="Logo" width="350" height="350">
  </a>
  <p align="center">
    Blocks harmful content and prohibits circumvention.
    <br />
  </p>
</div>



<!-- TABLE OF CONTENTS -->
<details>
  <summary>Table of Contents</summary>
  <ol>
    <li><a href="#about-the-project">About The Project</a></li>
    <li><a href="#configuration">Configuration</a></li>
    <li><a href="#troubleshooting">Troubleshooting</a></li>
    <li><a href="#contributing">Contributing</a></li>
    <li><a href="#license">License</a></li>
  </ol>
</details>

<!-- ABOUT THE PROJECT -->
## About The Project

SafeSurf for Windows can protect you and your loved ones from harmful materials online.

Configurable bypass prevention prevents the user from disabling the filter or making changes to settings for
a certain period of time.

SafeSurf is privacy friendly as absolutely no data is collected and it is completely free and open source.

<!-- GETTING STARTED -->
## Configuration

SafeSurf has been designed to be simple to use, simply download the most recent binary from [here](https://github.com/na-stewart/SafeSurf/releases) and run `SafeSurf`

<ins>Before configuration, make sure you understand what each setting does.</ins>

### Hosts Filter
Operating system level content filtering is implemented via a hosts file which is used to map a connection between an IP address and domain name. SafeSurf routes A massive amount of questionable domains to `0.0.0.0` to reject the user's request to visit the site. The main drawback is that some content may fall through the cracks if a domain is not yet registered with SafeSurf and may not be as accurate as CleanBrowsing. 

- Adult: Rejects all questionable adult content.
- Gambling: Rejects all online gambling sites and questionable adult content.
- Family: Rejects all questionable adult content as well as mixed content sites (like Reddit).

### CleanBrowsing DNS Filter
Network level content filtering is implemented via a service called [CleanBrowsing](https://cleanbrowsing.org/) which provides free, private DNS addresses that effectively reject questionable content. The main drawback is that setting a custom DNS on Windows may lead to network issues depending on your ISP. Some users may be uncomfortable funneling all network activity through CleanBrowsing, be sure to read their [privacy policy](https://cleanbrowsing.org/privacy) and investigate further.

- Adult: Rejects all questionable adult content.
- Family: Rejects all questionable adult content and blocks access to mixed content sites (like Reddit).

### Disable PowerShell
This setting is only utilized when the SafeSurf enforcer is active. If enabled, any Windows PowerShell instance will be closed in order to prevent circumvention. 

### Days Enforced
This setting will initialize the SafeSurf enforcer which is a program that prevents filter circumvention. It has been designed to be difficult for technical and non-technical users to get around. Once activated, any SafeSurf settings that have been enabled cannot be changed until the enforcer expires. It is highly recommended to start with 1 to 7 days and increase the time span once your settings are working well for you.

## Troubleshooting

### SafeSurf is being detected as a virus.
It may occur that SafeSurf is detected as a virus by your antivirus due to the anti-circumvention measures of the enforcer. To reiterate, SafeSurf is not a malicious program and does not cause harm in any way. 
This can be solved by making an exception for SafeSurf in your respective antivirus, make sure to do so with the program directory and not the program directly. 

### I cannot connect to the internet.
In the instance that CleanBrowsing causes connectivity issues, it is recommended to simply utilize the Windows network adapter troubleshooter. This will in most instances fix your connectivity issues.

### Why does SafeSurf ask for administrator priviliges?
Functionality including setting DNS, manipulating the hosts file, and some circumvention measures require elevation to function.

<!-- CONTRIBUTING -->
## Contributing

Contributions are what make the open source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion that would make this better, please fork the repo and create a pull request. You can also simply open an issue with the tag "enhancement".
Don't forget to give the project a star! Thanks again!

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

<!-- LICENSE -->
## License

Distributed under the MIT License. See `LICENSE.txt` for more information.
